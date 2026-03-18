use rand::seq::SliceRandom;
use std::sync::Arc;
use tokio::sync::mpsc;

use crate::models::{GameMode, MatchSession, QueueEntry, Team, TeamPlayer};
use crate::services::metrics::MetricsCollector;
use crate::services::{QueueManager, SessionManager};

#[derive(Debug, Clone)]
pub enum MatchmakingEvent {
    NewMatch(MatchSession),
    PlayerJoined {
        session: MatchSession,
        new_player: TeamPlayer,
        team_id: u8,
    },
}

pub struct Matchmaker {
    queue_manager: Arc<QueueManager>,
    session_manager: Arc<SessionManager>,
    #[allow(dead_code)]
    metrics: Arc<MetricsCollector>,
}

impl Matchmaker {
    pub fn new(
        queue_manager: Arc<QueueManager>,
        session_manager: Arc<SessionManager>,
        metrics: Arc<MetricsCollector>,
    ) -> Self {
        Self {
            queue_manager,
            session_manager,
            metrics,
        }
    }

    pub async fn run(
        self: Arc<Self>,
        interval_ms: u64,
        mut shutdown: mpsc::Receiver<()>,
        match_tx: mpsc::Sender<MatchmakingEvent>,
    ) {
        let interval = tokio::time::Duration::from_millis(interval_ms);

        loop {
            tokio::select! {
                _ = shutdown.recv() => {
                    tracing::info!("Matchmaker shutting down");
                    break;
                }
                _ = tokio::time::sleep(interval) => {
                    self.check_all_queues(&match_tx).await;
                }
            }
        }
    }

    async fn check_all_queues(&self, match_tx: &mpsc::Sender<MatchmakingEvent>) {
        let modes = [
            GameMode::OneVsOne,
            GameMode::TwoVsTwo,
            GameMode::ThreeVsThree,
            GameMode::FourVsFour,
        ];

        for mode in modes {
            // First: try to fill open sessions with queued players
            if let Some(open_session_id) = self.session_manager.get_open_session_for_mode(mode).await {
                loop {
                    let entry = match self.queue_manager.dequeue_one(mode).await {
                        Some(e) => e,
                        None => break,
                    };

                    if let Some((session, new_player, team_id)) =
                        self.session_manager.add_player_to_open_session(open_session_id, entry).await
                    {
                        tracing::info!(
                            "Player {} joined open session {} ({:?})",
                            new_player.username,
                            session.id,
                            session.game_mode
                        );

                        if match_tx.send(MatchmakingEvent::PlayerJoined {
                            session: session.clone(),
                            new_player,
                            team_id,
                        }).await.is_err() {
                            tracing::error!("Failed to send player joined notification");
                        }

                        // Stop if session is now full
                        if !session.is_open {
                            break;
                        }
                    } else {
                        break;
                    }
                }
            }

            // Then: try to create new matches from remaining queue
            if let Some(players) = self.queue_manager.try_match(mode).await {
                let mut session = self.create_match(mode, players);

                // If partial match, mark as open for late joins
                let total_players: usize = session.teams.iter().map(|t| t.players.len()).sum();
                if total_players < mode.players_required() {
                    session.is_open = true;
                }

                tracing::info!(
                    "Match created: {} ({:?}, open={})",
                    session.id,
                    session.game_mode,
                    session.is_open
                );
                self.session_manager.add_session(session.clone()).await;

                if match_tx.send(MatchmakingEvent::NewMatch(session)).await.is_err() {
                    tracing::error!("Failed to send match notification");
                }
            }
        }
    }

    fn create_match(&self, game_mode: GameMode, mut players: Vec<QueueEntry>) -> MatchSession {
        let mut rng = rand::thread_rng();
        players.shuffle(&mut rng);

        let num_teams = 2usize;
        let mut teams: Vec<Vec<TeamPlayer>> = (0..num_teams).map(|_| Vec::new()).collect();
        let mut game_player_id: u8 = 0;

        // Round-robin distribution so humans spread evenly across teams
        for (i, entry) in players.iter().enumerate() {
            teams[i % num_teams].push(TeamPlayer {
                player_id: entry.player_id,
                username: entry.username.clone(),
                game_player_id,
                is_ready: false,
                civilization: entry.civilization,
            });
            game_player_id += 1;
        }

        let team_structs: Vec<Team> = teams
            .into_iter()
            .enumerate()
            .map(|(idx, players)| Team {
                team_id: idx as u8,
                players,
            })
            .collect();

        MatchSession::new(game_mode, team_structs)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::models::ConnectionInfo;
    use uuid::Uuid;

    fn create_players(count: usize, game_mode: GameMode) -> Vec<QueueEntry> {
        (0..count)
            .map(|i| {
                QueueEntry::new(
                    Uuid::new_v4(),
                    format!("Player{}", i + 1),
                    game_mode,
                    0,
                )
            })
            .collect()
    }

    #[tokio::test]
    async fn test_create_match_1v1() {
        let queue_manager = QueueManager::new();
        let session_manager = SessionManager::new();
        let metrics = MetricsCollector::new();
        let matchmaker = Matchmaker::new(queue_manager, session_manager, metrics);

        let players = create_players(2, GameMode::OneVsOne);
        let session = matchmaker.create_match(GameMode::OneVsOne, players);

        assert_eq!(session.teams.len(), 2);
        assert_eq!(session.teams[0].players.len(), 1);
        assert_eq!(session.teams[1].players.len(), 1);
        assert!(matches!(session.connection_info, ConnectionInfo::Relay { .. }));
    }

    #[tokio::test]
    async fn test_create_match_2v2() {
        let queue_manager = QueueManager::new();
        let session_manager = SessionManager::new();
        let metrics = MetricsCollector::new();
        let matchmaker = Matchmaker::new(queue_manager, session_manager, metrics);

        let players = create_players(4, GameMode::TwoVsTwo);
        let session = matchmaker.create_match(GameMode::TwoVsTwo, players);

        assert_eq!(session.teams.len(), 2);
        assert_eq!(session.teams[0].players.len(), 2);
        assert_eq!(session.teams[1].players.len(), 2);
        assert!(matches!(session.connection_info, ConnectionInfo::Relay { .. }));
    }

    #[tokio::test]
    async fn test_create_match_3v3() {
        let queue_manager = QueueManager::new();
        let session_manager = SessionManager::new();
        let metrics = MetricsCollector::new();
        let matchmaker = Matchmaker::new(queue_manager, session_manager, metrics);

        let players = create_players(6, GameMode::ThreeVsThree);
        let session = matchmaker.create_match(GameMode::ThreeVsThree, players);

        assert_eq!(session.teams.len(), 2);
        assert_eq!(session.teams[0].players.len(), 3);
        assert_eq!(session.teams[1].players.len(), 3);
        assert!(matches!(session.connection_info, ConnectionInfo::Relay { .. }));
    }

    #[tokio::test]
    async fn test_create_match_4v4() {
        let queue_manager = QueueManager::new();
        let session_manager = SessionManager::new();
        let metrics = MetricsCollector::new();
        let matchmaker = Matchmaker::new(queue_manager, session_manager, metrics);

        let players = create_players(8, GameMode::FourVsFour);
        let session = matchmaker.create_match(GameMode::FourVsFour, players);

        assert_eq!(session.teams.len(), 2);
        assert_eq!(session.teams[0].players.len(), 4);
        assert_eq!(session.teams[1].players.len(), 4);
        assert!(matches!(session.connection_info, ConnectionInfo::Relay { .. }));
    }

    #[tokio::test]
    async fn test_partial_match_2v2_with_2_humans() {
        let queue_manager = QueueManager::new();
        let session_manager = SessionManager::new();
        let metrics = MetricsCollector::new();
        let matchmaker = Matchmaker::new(queue_manager, session_manager, metrics);

        // 2 humans in a 2v2 — round-robin puts 1 per team
        let players = create_players(2, GameMode::TwoVsTwo);
        let session = matchmaker.create_match(GameMode::TwoVsTwo, players);

        assert_eq!(session.teams.len(), 2);
        assert_eq!(session.teams[0].players.len(), 1);
        assert_eq!(session.teams[1].players.len(), 1);

        // IDs should be 0 and 1
        let mut ids: Vec<u8> = session.all_players().iter().map(|p| p.game_player_id).collect();
        ids.sort();
        assert_eq!(ids, vec![0, 1]);
    }

    #[tokio::test]
    async fn test_partial_match_3v3_with_3_humans() {
        let queue_manager = QueueManager::new();
        let session_manager = SessionManager::new();
        let metrics = MetricsCollector::new();
        let matchmaker = Matchmaker::new(queue_manager, session_manager, metrics);

        // 3 humans in a 3v3 — round-robin: team0 gets [0,2], team1 gets [1]
        let players = create_players(3, GameMode::ThreeVsThree);
        let session = matchmaker.create_match(GameMode::ThreeVsThree, players);

        assert_eq!(session.teams.len(), 2);
        assert_eq!(session.teams[0].players.len(), 2);
        assert_eq!(session.teams[1].players.len(), 1);
    }

    #[tokio::test]
    async fn test_game_player_ids() {
        let queue_manager = QueueManager::new();
        let session_manager = SessionManager::new();
        let metrics = MetricsCollector::new();
        let matchmaker = Matchmaker::new(queue_manager, session_manager, metrics);

        let players = create_players(8, GameMode::FourVsFour);
        let session = matchmaker.create_match(GameMode::FourVsFour, players);

        let all_players = session.all_players();
        let mut game_player_ids: Vec<u8> = all_players.iter().map(|p| p.game_player_id).collect();
        game_player_ids.sort();

        assert_eq!(game_player_ids, vec![0, 1, 2, 3, 4, 5, 6, 7]);
    }
}
