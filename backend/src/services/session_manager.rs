use std::collections::{HashMap, HashSet};
use std::sync::Arc;
use tokio::sync::RwLock;
use tokio::time::Instant;
use uuid::Uuid;

use crate::models::{GameMode, MatchSession, MatchState, QueueEntry, TeamPlayer};
use crate::services::metrics::SessionCounts;

pub struct SessionManager {
    sessions: RwLock<HashMap<Uuid, MatchSession>>,
    player_sessions: RwLock<HashMap<Uuid, Uuid>>,
    disconnected_players: RwLock<HashMap<Uuid, Instant>>,
}

impl SessionManager {
    pub fn new() -> Arc<Self> {
        Arc::new(Self {
            sessions: RwLock::new(HashMap::new()),
            player_sessions: RwLock::new(HashMap::new()),
            disconnected_players: RwLock::new(HashMap::new()),
        })
    }

    pub async fn add_session(&self, session: MatchSession) {
        let session_id = session.id;
        let player_ids = session.all_player_ids();

        {
            let mut sessions = self.sessions.write().await;
            sessions.insert(session_id, session);
        }

        {
            let mut player_sessions = self.player_sessions.write().await;
            for player_id in player_ids {
                player_sessions.insert(player_id, session_id);
            }
        }
    }

    pub async fn get_session(&self, session_id: Uuid) -> Option<MatchSession> {
        let sessions = self.sessions.read().await;
        sessions.get(&session_id).cloned()
    }

    pub async fn get_player_session(&self, player_id: Uuid) -> Option<MatchSession> {
        let session_id = {
            let player_sessions = self.player_sessions.read().await;
            player_sessions.get(&player_id).copied()?
        };
        self.get_session(session_id).await
    }

    pub async fn set_player_ready(&self, player_id: Uuid) -> Option<(MatchSession, bool)> {
        let session_id = {
            let player_sessions = self.player_sessions.read().await;
            player_sessions.get(&player_id).copied()?
        };

        let mut sessions = self.sessions.write().await;
        let session = sessions.get_mut(&session_id)?;
        session.set_player_ready(player_id);
        session.is_open = false; // Prevent new joins once readying up
        let all_ready = session.all_ready();
        if all_ready {
            session.state = MatchState::Starting;
        }
        Some((session.clone(), all_ready))
    }

    pub async fn set_host_address(&self, player_id: Uuid, address: String) -> Option<MatchSession> {
        let session_id = {
            let player_sessions = self.player_sessions.read().await;
            player_sessions.get(&player_id).copied()?
        };

        let mut sessions = self.sessions.write().await;
        let session = sessions.get_mut(&session_id)?;
        session.set_host_address(address);
        Some(session.clone())
    }

    pub async fn update_session_state(
        &self,
        session_id: Uuid,
        state: MatchState,
    ) -> Option<MatchSession> {
        let mut sessions = self.sessions.write().await;
        let session = sessions.get_mut(&session_id)?;
        session.state = state;
        Some(session.clone())
    }

    pub async fn get_open_session_for_mode(&self, game_mode: GameMode) -> Option<Uuid> {
        let sessions = self.sessions.read().await;
        for (id, session) in sessions.iter() {
            if session.is_open && session.game_mode == game_mode {
                return Some(*id);
            }
        }
        None
    }

    pub async fn add_player_to_open_session(
        &self,
        session_id: Uuid,
        entry: QueueEntry,
    ) -> Option<(MatchSession, TeamPlayer, u8)> {
        let mut sessions = self.sessions.write().await;
        let session = sessions.get_mut(&session_id)?;

        // Find team with fewest players (round-robin)
        let mut min_team_idx = 0;
        let mut min_count = usize::MAX;
        for (i, team) in session.teams.iter().enumerate() {
            if team.players.len() < min_count {
                min_count = team.players.len();
                min_team_idx = i;
            }
        }

        // Assign next game_player_id (max existing + 1)
        let max_id = session
            .all_players()
            .iter()
            .map(|p| p.game_player_id)
            .max()
            .unwrap_or(0);
        let game_player_id = max_id + 1;

        let team_id = session.teams[min_team_idx].team_id;

        let new_player = TeamPlayer {
            player_id: entry.player_id,
            username: entry.username.clone(),
            game_player_id,
            is_ready: false,
            civilization: entry.civilization,
        };

        session.teams[min_team_idx].players.push(new_player.clone());

        // Check if session is now full
        let total_players: usize = session.teams.iter().map(|t| t.players.len()).sum();
        if total_players >= session.game_mode.players_required() {
            session.is_open = false;
        }

        let updated_session = session.clone();

        // Update player_sessions mapping (need to drop sessions lock first)
        drop(sessions);
        {
            let mut player_sessions = self.player_sessions.write().await;
            player_sessions.insert(entry.player_id, session_id);
        }

        Some((updated_session, new_player, team_id))
    }

    pub async fn mark_player_disconnected(&self, player_id: Uuid) {
        self.disconnected_players.write().await.insert(player_id, Instant::now());
    }

    pub async fn mark_player_reconnected(&self, player_id: Uuid) {
        self.disconnected_players.write().await.remove(&player_id);
    }

    pub async fn is_player_disconnected(&self, player_id: Uuid) -> bool {
        self.disconnected_players.read().await.contains_key(&player_id)
    }

    pub async fn get_player_session_if_active(&self, player_id: Uuid) -> Option<MatchSession> {
        let session_id = {
            let player_sessions = self.player_sessions.read().await;
            player_sessions.get(&player_id).copied()?
        };
        let sessions = self.sessions.read().await;
        let session = sessions.get(&session_id)?;
        if session.state == MatchState::InProgress {
            Some(session.clone())
        } else {
            None
        }
    }

    pub async fn has_any_connected_players(&self, session_id: Uuid) -> bool {
        let sessions = self.sessions.read().await;
        let session = match sessions.get(&session_id) {
            Some(s) => s,
            None => return false,
        };
        let disconnected = self.disconnected_players.read().await;
        let player_sessions = self.player_sessions.read().await;
        for pid in session.all_player_ids() {
            // Player is still connected if they're in player_sessions and NOT in disconnected_players
            if player_sessions.contains_key(&pid) && !disconnected.contains_key(&pid) {
                return true;
            }
        }
        false
    }

    pub async fn remove_player_from_session(&self, player_id: Uuid) -> Option<MatchSession> {
        let session_id = {
            let mut player_sessions = self.player_sessions.write().await;
            player_sessions.remove(&player_id)?
        };

        let sessions = self.sessions.read().await;
        sessions.get(&session_id).cloned()
    }

    pub async fn remove_session(&self, session_id: Uuid) {
        let session = {
            let mut sessions = self.sessions.write().await;
            sessions.remove(&session_id)
        };

        if let Some(session) = session {
            let mut player_sessions = self.player_sessions.write().await;
            for player_id in session.all_player_ids() {
                player_sessions.remove(&player_id);
            }
        }
    }

    pub async fn complete_session(&self, session_id: Uuid) {
        // First set state to Finished
        {
            let mut sessions = self.sessions.write().await;
            if let Some(session) = sessions.get_mut(&session_id) {
                session.state = MatchState::Finished;
            }
        }
        // Then remove the session
        self.remove_session(session_id).await;
    }

    pub async fn get_session_counts(&self) -> SessionCounts {
        let sessions = self.sessions.read().await;
        let mut waiting_for_ready = 0;
        let mut starting = 0;
        let mut in_progress = 0;

        for session in sessions.values() {
            match session.state {
                MatchState::WaitingForReady => waiting_for_ready += 1,
                MatchState::Starting => starting += 1,
                MatchState::InProgress => in_progress += 1,
                MatchState::Finished => {}
            }
        }

        SessionCounts {
            waiting_for_ready,
            starting,
            in_progress,
            total: sessions.len(),
        }
    }

    pub async fn get_players_in_matches(&self) -> usize {
        let sessions = self.sessions.read().await;
        sessions
            .values()
            .filter(|s| {
                matches!(
                    s.state,
                    MatchState::WaitingForReady | MatchState::Starting | MatchState::InProgress
                )
            })
            .map(|s| s.all_players().len())
            .sum()
    }
}

impl Default for SessionManager {
    fn default() -> Self {
        Self {
            sessions: RwLock::new(HashMap::new()),
            player_sessions: RwLock::new(HashMap::new()),
            disconnected_players: RwLock::new(HashMap::new()),
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::models::{GameMode, Team, TeamPlayer};

    fn create_test_session(player_count: usize) -> (MatchSession, Vec<Uuid>) {
        let player_ids: Vec<Uuid> = (0..player_count).map(|_| Uuid::new_v4()).collect();
        let team_size = player_count / 2;

        let teams = vec![
            Team {
                team_id: 0,
                players: player_ids[..team_size]
                    .iter()
                    .enumerate()
                    .map(|(i, &id)| TeamPlayer {
                        player_id: id,
                        username: format!("Player{}", i + 1),
                        game_player_id: i as u8,
                        is_ready: false,
                        civilization: 0,
                    })
                    .collect(),
            },
            Team {
                team_id: 1,
                players: player_ids[team_size..]
                    .iter()
                    .enumerate()
                    .map(|(i, &id)| TeamPlayer {
                        player_id: id,
                        username: format!("Player{}", team_size + i + 1),
                        game_player_id: (team_size + i) as u8,
                        is_ready: false,
                        civilization: 0,
                    })
                    .collect(),
            },
        ];

        let game_mode = match player_count {
            2 => GameMode::OneVsOne,
            4 => GameMode::TwoVsTwo,
            6 => GameMode::ThreeVsThree,
            8 => GameMode::FourVsFour,
            _ => GameMode::OneVsOne,
        };

        (MatchSession::new(game_mode, teams), player_ids)
    }

    #[tokio::test]
    async fn test_add_and_get_session() {
        let manager = SessionManager::new();
        let (session, _) = create_test_session(2);
        let session_id = session.id;

        manager.add_session(session.clone()).await;

        let retrieved = manager.get_session(session_id).await;
        assert!(retrieved.is_some());
        assert_eq!(retrieved.unwrap().id, session_id);
    }

    #[tokio::test]
    async fn test_get_player_session() {
        let manager = SessionManager::new();
        let (session, player_ids) = create_test_session(2);
        let session_id = session.id;

        manager.add_session(session).await;

        let retrieved = manager.get_player_session(player_ids[0]).await;
        assert!(retrieved.is_some());
        assert_eq!(retrieved.unwrap().id, session_id);
    }

    #[tokio::test]
    async fn test_player_ready() {
        let manager = SessionManager::new();
        let (session, player_ids) = create_test_session(2);

        manager.add_session(session).await;

        let result = manager.set_player_ready(player_ids[0]).await;
        assert!(result.is_some());

        let (updated_session, all_ready) = result.unwrap();
        let player = updated_session
            .all_players()
            .into_iter()
            .find(|p| p.player_id == player_ids[0])
            .unwrap();

        assert!(player.is_ready);
        assert!(!all_ready);
    }

    #[tokio::test]
    async fn test_all_players_ready() {
        let manager = SessionManager::new();
        let (session, player_ids) = create_test_session(2);

        manager.add_session(session).await;

        // Set first player ready
        manager.set_player_ready(player_ids[0]).await;

        // Set second player ready - should trigger Starting state
        let result = manager.set_player_ready(player_ids[1]).await;
        assert!(result.is_some());

        let (updated_session, all_ready) = result.unwrap();
        assert!(all_ready);
        assert_eq!(updated_session.state, MatchState::Starting);
    }

    #[tokio::test]
    async fn test_remove_player() {
        let manager = SessionManager::new();
        let (session, player_ids) = create_test_session(4);
        let session_id = session.id;

        manager.add_session(session).await;

        // Remove one player
        let result = manager.remove_player_from_session(player_ids[0]).await;
        assert!(result.is_some());

        // Session should still exist
        let session = manager.get_session(session_id).await;
        assert!(session.is_some());

        // But removed player should no longer be associated
        let player_session = manager.get_player_session(player_ids[0]).await;
        assert!(player_session.is_none());

        // Other players should still be associated
        let other_session = manager.get_player_session(player_ids[1]).await;
        assert!(other_session.is_some());
    }

    #[tokio::test]
    async fn test_open_session_management() {
        let manager = SessionManager::new();
        let (mut session, _player_ids) = create_test_session(2);

        // Mark session as open (partial 2v2 with 2 humans)
        session.game_mode = GameMode::TwoVsTwo;
        session.is_open = true;
        let session_id = session.id;
        manager.add_session(session).await;

        // Should find open session for TwoVsTwo
        let found = manager.get_open_session_for_mode(GameMode::TwoVsTwo).await;
        assert_eq!(found, Some(session_id));

        // Should NOT find open session for OneVsOne
        let not_found = manager.get_open_session_for_mode(GameMode::OneVsOne).await;
        assert!(not_found.is_none());

        // Add a new player
        let new_player_id = Uuid::new_v4();
        let entry = crate::models::QueueEntry::new(new_player_id, "NewPlayer".to_string(), GameMode::TwoVsTwo, 0);

        let result = manager.add_player_to_open_session(session_id, entry).await;
        assert!(result.is_some());

        let (updated_session, new_player, team_id) = result.unwrap();
        assert_eq!(new_player.game_player_id, 2); // existing are 0 and 1
        assert!(team_id == 0 || team_id == 1);

        // Total players should be 3
        assert_eq!(updated_session.all_players().len(), 3);
        // Session should still be open (need 4 for 2v2)
        assert!(updated_session.is_open);

        // Add one more to fill it
        let fourth_player_id = Uuid::new_v4();
        let entry2 = crate::models::QueueEntry::new(fourth_player_id, "FourthPlayer".to_string(), GameMode::TwoVsTwo, 0);
        let result2 = manager.add_player_to_open_session(session_id, entry2).await;
        assert!(result2.is_some());

        let (full_session, _, _) = result2.unwrap();
        assert_eq!(full_session.all_players().len(), 4);
        // Session should now be closed (full)
        assert!(!full_session.is_open);

        // Should no longer find an open session
        let no_open = manager.get_open_session_for_mode(GameMode::TwoVsTwo).await;
        assert!(no_open.is_none());
    }

    #[tokio::test]
    async fn test_ready_closes_open_session() {
        let manager = SessionManager::new();
        let (mut session, player_ids) = create_test_session(2);
        session.game_mode = GameMode::TwoVsTwo;
        session.is_open = true;
        manager.add_session(session).await;

        // Setting a player ready should close the session
        manager.set_player_ready(player_ids[0]).await;

        let found = manager.get_open_session_for_mode(GameMode::TwoVsTwo).await;
        assert!(found.is_none());
    }
}