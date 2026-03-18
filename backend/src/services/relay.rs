use std::collections::{HashMap, HashSet};
use std::sync::Arc;
use std::time::{Duration, Instant};
use tokio::sync::{mpsc, RwLock};
use uuid::Uuid;

use crate::models::{GameCommand, ServerMessage};

type CommandSender = mpsc::UnboundedSender<ServerMessage>;

pub struct RelaySession {
    #[allow(dead_code)]
    pub session_id: Uuid,
    pub players: HashMap<Uuid, CommandSender>,
    pub player_count: usize,

    // Frame synchronization: buffer commands until all players submit for a frame
    frame_commands: HashMap<u32, Vec<(u8, GameCommand)>>,  // frame -> [(game_player_id, cmd)]
    players_submitted: HashMap<u32, HashSet<u8>>,          // frame -> set of game_player_ids

    // Deadline-based broadcast
    frame_first_submission: HashMap<u32, Instant>,  // when first player submitted for each frame
    broadcast_frames: HashSet<u32>,                  // frames already broadcast (to drop late commands)
    game_player_ids: HashSet<u8>,                    // all player IDs in session
    backgrounded_players: HashSet<u8>,               // players with hidden/backgrounded tabs
    deadline_duration: Duration,                     // 500ms default

    // Reconnect support
    command_history: Vec<(u32, Vec<(u8, GameCommand)>)>,  // (frame, commands) in broadcast order
    disconnected_game_player_ids: HashSet<u8>,
}

impl RelaySession {
    pub fn new(session_id: Uuid, player_count: usize) -> Self {
        Self {
            session_id,
            players: HashMap::new(),
            player_count,
            frame_commands: HashMap::new(),
            players_submitted: HashMap::new(),
            frame_first_submission: HashMap::new(),
            broadcast_frames: HashSet::new(),
            game_player_ids: HashSet::new(),
            backgrounded_players: HashSet::new(),
            deadline_duration: Duration::from_millis(500),
            command_history: Vec::new(),
            disconnected_game_player_ids: HashSet::new(),
        }
    }

    pub fn add_player(&mut self, player_id: Uuid, game_player_id: u8, sender: CommandSender) {
        self.players.insert(player_id, sender);
        self.game_player_ids.insert(game_player_id);
    }

    pub fn remove_player(&mut self, player_id: Uuid, game_player_id: Option<u8>) {
        self.players.remove(&player_id);
        if let Some(gid) = game_player_id {
            self.backgrounded_players.remove(&gid);
        }
    }

    pub fn set_player_backgrounded(&mut self, game_player_id: u8, backgrounded: bool) {
        if backgrounded {
            self.backgrounded_players.insert(game_player_id);
        } else {
            self.backgrounded_players.remove(&game_player_id);
        }
        tracing::info!(
            game_player_id,
            backgrounded,
            "Player tab visibility changed"
        );
    }

    pub fn active_player_count(&self) -> usize {
        let active = self.player_count
            .saturating_sub(self.backgrounded_players.len())
            .saturating_sub(self.disconnected_game_player_ids.len());
        if active == 0 {
            // All players backgrounded/disconnected — fall back to 1 so deadline logic still works
            1
        } else {
            active
        }
    }

    pub fn broadcast(&self, message: ServerMessage, exclude: Option<Uuid>) {
        for (player_id, sender) in &self.players {
            if exclude.map_or(true, |ex| ex != *player_id) {
                let _ = sender.send(message.clone());
            }
        }
    }

    /// Buffer a command for a frame. When all players have submitted for a frame, broadcast all commands.
    /// Players signal "tick complete" by sending a Noop command.
    /// Returns Some(frame) if a deadline timer should be started for this frame.
    pub fn buffer_command(&mut self, from_game_player_id: u8, command: GameCommand) -> Option<u32> {
        let frame = command.frame;

        // Drop commands for frames that have already been broadcast
        if self.broadcast_frames.contains(&frame) {
            return None;
        }

        // Add to frame buffer
        self.frame_commands
            .entry(frame)
            .or_insert_with(Vec::new)
            .push((from_game_player_id, command.clone()));

        // Only mark player as submitted when they send Noop (signals tick complete)
        if command.command_type == "Noop" {
            self.players_submitted
                .entry(frame)
                .or_insert_with(HashSet::new)
                .insert(from_game_player_id);

            // Track when the first player submits for this frame
            let is_first = !self.frame_first_submission.contains_key(&frame);
            if is_first {
                self.frame_first_submission.insert(frame, Instant::now());
            }

            // Check if all active players have submitted for this frame
            if let Some(submitted) = self.players_submitted.get(&frame) {
                if submitted.len() >= self.active_player_count() {
                    // Use force_broadcast_frame to inject synthetic Noops for backgrounded players
                    self.force_broadcast_frame(frame);
                    return None;
                }
            }

            // If this was the first submission, signal that a deadline timer should start
            if is_first {
                return Some(frame);
            }
        }

        None
    }

    /// Force broadcast a frame when the deadline expires.
    /// Injects synthetic Noops for any players who haven't submitted.
    pub fn force_broadcast_frame(&mut self, frame: u32) {
        // Already broadcast (all players submitted before deadline)
        if self.broadcast_frames.contains(&frame) {
            return;
        }

        // Find players who haven't submitted
        let submitted = self.players_submitted.get(&frame)
            .cloned()
            .unwrap_or_default();

        let missing: Vec<u8> = self.game_player_ids.iter()
            .filter(|id| !submitted.contains(id))
            .copied()
            .collect();

        if !missing.is_empty() {
            tracing::warn!(
                frame = frame,
                ?missing,
                "Deadline expired: injecting synthetic Noops for missing players"
            );
        }

        for &game_player_id in &self.game_player_ids {
            if !submitted.contains(&game_player_id) {
                // Inject synthetic Noop with checksum=0, simTick=0
                let synthetic_noop = GameCommand {
                    frame,
                    command_type: "Noop".to_string(),
                    payload: serde_json::json!({
                        "checksum": 0,
                        "simTick": 0
                    }),
                };

                self.frame_commands
                    .entry(frame)
                    .or_insert_with(Vec::new)
                    .push((game_player_id, synthetic_noop));

                self.players_submitted
                    .entry(frame)
                    .or_insert_with(HashSet::new)
                    .insert(game_player_id);
            }
        }

        self.broadcast_frame(frame);
    }

    /// Broadcast all buffered commands for a frame to all players
    fn broadcast_frame(&mut self, frame: u32) {
        if let Some(commands) = self.frame_commands.remove(&frame) {
            // Save to command history for reconnect
            self.command_history.push((frame, commands.clone()));

            for (from_player_id, command) in commands {
                let message = ServerMessage::GameCommand {
                    from_player_id,
                    command,
                };
                self.broadcast(message, None);
            }
        }
        self.players_submitted.remove(&frame);
        self.frame_first_submission.remove(&frame);
        self.broadcast_frames.insert(frame);

        // Prune old broadcast_frames entries to prevent unbounded growth
        // Keep only frames within 1000 of the current frame
        if self.broadcast_frames.len() > 1500 {
            let max_frame = self.broadcast_frames.iter().copied().max().unwrap_or(0);
            self.broadcast_frames.retain(|&f| max_frame - f < 1000);
        }
    }

    pub fn soft_disconnect_player(&mut self, player_id: Uuid, game_player_id: Option<u8>) {
        self.players.remove(&player_id);
        if let Some(gid) = game_player_id {
            self.disconnected_game_player_ids.insert(gid);
            self.backgrounded_players.remove(&gid);
        }
        // Keep game_player_ids, mappings intact for reconnect
    }

    pub fn reconnect_player(&mut self, player_id: Uuid, sender: CommandSender) {
        self.players.insert(player_id, sender);
        // Find the game_player_id for this player and remove from disconnected set
        // (caller should provide it via RelayManager)
    }

    pub fn remove_from_disconnected(&mut self, game_player_id: u8) {
        self.disconnected_game_player_ids.remove(&game_player_id);
    }

    pub fn command_history(&self) -> &[(u32, Vec<(u8, GameCommand)>)] {
        &self.command_history
    }

    pub fn deadline_duration(&self) -> Duration {
        self.deadline_duration
    }
}

pub struct RelayManager {
    sessions: RwLock<HashMap<Uuid, RelaySession>>,
    player_to_session: RwLock<HashMap<Uuid, Uuid>>,
    player_game_ids: RwLock<HashMap<Uuid, u8>>,
    active_deadlines: Arc<RwLock<HashSet<(Uuid, u32)>>>,
}

impl RelayManager {
    pub fn new() -> Arc<Self> {
        Arc::new(Self {
            sessions: RwLock::new(HashMap::new()),
            player_to_session: RwLock::new(HashMap::new()),
            player_game_ids: RwLock::new(HashMap::new()),
            active_deadlines: Arc::new(RwLock::new(HashSet::new())),
        })
    }

    pub async fn create_session(&self, session_id: Uuid, player_count: usize) {
        let mut sessions = self.sessions.write().await;
        sessions.insert(session_id, RelaySession::new(session_id, player_count));
    }

    pub async fn add_player_to_session(
        &self,
        session_id: Uuid,
        player_id: Uuid,
        game_player_id: u8,
        sender: CommandSender,
    ) {
        {
            let mut sessions = self.sessions.write().await;
            if let Some(session) = sessions.get_mut(&session_id) {
                session.add_player(player_id, game_player_id, sender);
            }
        }

        {
            let mut mapping = self.player_to_session.write().await;
            mapping.insert(player_id, session_id);
        }

        {
            let mut game_ids = self.player_game_ids.write().await;
            game_ids.insert(player_id, game_player_id);
        }
    }

    pub async fn relay_command(self: &Arc<Self>, player_id: Uuid, command: GameCommand) {
        let session_id = {
            let mapping = self.player_to_session.read().await;
            mapping.get(&player_id).copied()
        };

        let game_player_id = {
            let game_ids = self.player_game_ids.read().await;
            match game_ids.get(&player_id).copied() {
                Some(id) => id,
                None => {
                    tracing::warn!(
                        %player_id,
                        "No game_player_id mapping found in relay_command, defaulting to 0"
                    );
                    0
                }
            }
        };

        if let Some(session_id) = session_id {
            let (needs_deadline, deadline_duration) = {
                let mut sessions = self.sessions.write().await;
                if let Some(session) = sessions.get_mut(&session_id) {
                    let result = session.buffer_command(game_player_id, command);
                    let duration = session.deadline_duration();
                    (result, duration)
                } else {
                    (None, Duration::from_millis(100))
                }
            };

            // Spawn deadline timer if this is the first submission for a frame
            if let Some(frame) = needs_deadline {
                let deadline_key = (session_id, frame);

                // Check if a deadline is already active for this frame
                let already_active = {
                    let deadlines = self.active_deadlines.read().await;
                    deadlines.contains(&deadline_key)
                };

                if !already_active {
                    {
                        let mut deadlines = self.active_deadlines.write().await;
                        deadlines.insert(deadline_key);
                    }

                    let manager = Arc::clone(self);
                    let active_deadlines = Arc::clone(&self.active_deadlines);

                    tokio::spawn(async move {
                        tokio::time::sleep(deadline_duration).await;

                        // Force broadcast the frame
                        {
                            let mut sessions = manager.sessions.write().await;
                            if let Some(session) = sessions.get_mut(&session_id) {
                                session.force_broadcast_frame(frame);
                            }
                        }

                        // Clean up deadline tracking
                        {
                            let mut deadlines = active_deadlines.write().await;
                            deadlines.remove(&deadline_key);
                        }
                    });
                }
            }
        }
    }

    pub async fn broadcast_to_session(&self, session_id: Uuid, message: ServerMessage) {
        let sessions = self.sessions.read().await;
        if let Some(session) = sessions.get(&session_id) {
            session.broadcast(message, None);
        }
    }

    pub async fn remove_player(&self, player_id: Uuid) -> Option<Uuid> {
        let session_id = {
            let mut mapping = self.player_to_session.write().await;
            mapping.remove(&player_id)
        };

        let game_player_id = {
            let mut game_ids = self.player_game_ids.write().await;
            game_ids.remove(&player_id)
        };

        if let Some(session_id) = session_id {
            let mut sessions = self.sessions.write().await;
            if let Some(session) = sessions.get_mut(&session_id) {
                session.remove_player(player_id, game_player_id);
            }
        }

        session_id
    }

    pub async fn set_player_backgrounded(&self, player_id: Uuid, backgrounded: bool) {
        let session_id = {
            let mapping = self.player_to_session.read().await;
            mapping.get(&player_id).copied()
        };

        let game_player_id = {
            let game_ids = self.player_game_ids.read().await;
            game_ids.get(&player_id).copied()
        };

        if let (Some(session_id), Some(game_player_id)) = (session_id, game_player_id) {
            let mut sessions = self.sessions.write().await;
            if let Some(session) = sessions.get_mut(&session_id) {
                session.set_player_backgrounded(game_player_id, backgrounded);
            }
        }
    }

    pub async fn broadcast_player_ping(&self, player_id: Uuid, ping_ms: u32) {
        let session_id = {
            let mapping = self.player_to_session.read().await;
            mapping.get(&player_id).copied()
        };

        let game_player_id = {
            let game_ids = self.player_game_ids.read().await;
            match game_ids.get(&player_id).copied() {
                Some(id) => id,
                None => {
                    tracing::warn!(
                        %player_id,
                        "No game_player_id mapping found in broadcast_player_ping, defaulting to 0"
                    );
                    0
                }
            }
        };

        if let Some(session_id) = session_id {
            let sessions = self.sessions.read().await;
            if let Some(session) = sessions.get(&session_id) {
                session.broadcast(
                    crate::models::ServerMessage::PlayerPing { game_player_id, ping_ms },
                    None,
                );
            }
        }
    }

    pub async fn update_session_player_count(&self, session_id: Uuid, new_count: usize) {
        let mut sessions = self.sessions.write().await;
        if let Some(session) = sessions.get_mut(&session_id) {
            session.player_count = new_count;
        }
    }

    pub async fn soft_disconnect_player(&self, player_id: Uuid) -> Option<Uuid> {
        let session_id = {
            let mapping = self.player_to_session.read().await;
            mapping.get(&player_id).copied()
        };

        let game_player_id = {
            let game_ids = self.player_game_ids.read().await;
            game_ids.get(&player_id).copied()
        };

        if let Some(session_id) = session_id {
            let mut sessions = self.sessions.write().await;
            if let Some(session) = sessions.get_mut(&session_id) {
                session.soft_disconnect_player(player_id, game_player_id);
            }
        }

        // Keep player_to_session and player_game_ids mappings intact
        session_id
    }

    pub async fn reconnect_player(&self, player_id: Uuid, sender: CommandSender) -> Vec<(u32, Vec<(u8, GameCommand)>)> {
        let session_id = {
            let mapping = self.player_to_session.read().await;
            mapping.get(&player_id).copied()
        };

        let game_player_id = {
            let game_ids = self.player_game_ids.read().await;
            game_ids.get(&player_id).copied()
        };

        if let Some(session_id) = session_id {
            let mut sessions = self.sessions.write().await;
            if let Some(session) = sessions.get_mut(&session_id) {
                session.reconnect_player(player_id, sender);
                if let Some(gid) = game_player_id {
                    session.remove_from_disconnected(gid);
                }
                return session.command_history().to_vec();
            }
        }

        Vec::new()
    }

    pub async fn get_game_player_id(&self, player_id: Uuid) -> Option<u8> {
        let game_ids = self.player_game_ids.read().await;
        game_ids.get(&player_id).copied()
    }

    pub async fn remove_session(&self, session_id: Uuid) {
        let session = {
            let mut sessions = self.sessions.write().await;
            sessions.remove(&session_id)
        };

        if let Some(session) = session {
            let mut mapping = self.player_to_session.write().await;
            let mut game_ids = self.player_game_ids.write().await;
            for player_id in session.players.keys() {
                mapping.remove(player_id);
                game_ids.remove(player_id);
            }
        }
    }
}

impl Default for RelayManager {
    fn default() -> Self {
        Self {
            sessions: RwLock::new(HashMap::new()),
            player_to_session: RwLock::new(HashMap::new()),
            player_game_ids: RwLock::new(HashMap::new()),
            active_deadlines: Arc::new(RwLock::new(HashSet::new())),
        }
    }
}
