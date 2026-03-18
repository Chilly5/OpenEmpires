use std::collections::{HashMap, VecDeque};
use std::sync::Arc;
use std::time::{Instant, SystemTime, UNIX_EPOCH};
use tokio::sync::RwLock;
use uuid::Uuid;
use chrono::Utc;

use crate::models::GameMode;
use crate::services::MetricsDb;

const QUEUE_HISTORY_SIZE: usize = 1800; // 30 minutes at 1-second intervals
const MATCH_EVENT_HISTORY_SIZE: usize = 5000;
const DAILY_MATCH_HISTORY_SIZE: usize = 7; // 7 days
const FAILURE_HISTORY_SIZE: usize = 100;

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum MatchEventType {
    Created,
    Started,
    Completed,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum MatchResult {
    Victory { winning_team: u8 },
    Surrender,
    Draw,
    Abandoned,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum FailureType {
    MatchTimeout,
    PlayerDisconnect,
    RelayError,
}

impl std::fmt::Display for FailureType {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            FailureType::MatchTimeout => write!(f, "MatchTimeout"),
            FailureType::PlayerDisconnect => write!(f, "PlayerDisconnect"),
            FailureType::RelayError => write!(f, "RelayError"),
        }
    }
}

#[derive(Debug, Clone)]
pub struct MatchEvent {
    pub timestamp: u64,
    pub wall_clock_time: u64,
    pub game_mode: GameMode,
    pub event_type: MatchEventType,
    pub match_id: Option<Uuid>,
    pub duration_ms: Option<u64>,
    pub result: Option<MatchResult>,
    pub participant_countries: Vec<String>,
    pub participant_usernames: Vec<String>,
    pub participant_cities: Vec<String>,
}

#[derive(Debug, Clone)]
pub struct QueueSnapshot {
    pub timestamp: u64,
    pub queue_sizes: HashMap<GameMode, usize>,
    pub longest_wait_ms: HashMap<GameMode, u64>,
}

#[derive(Debug, Clone)]
pub struct ServerLoadMetrics {
    pub active_connections: usize,
    pub players_in_queue: usize,
    pub players_in_match: usize,
    pub active_sessions: usize,
}

#[derive(Debug, Clone)]
pub struct SessionCounts {
    pub waiting_for_ready: usize,
    pub starting: usize,
    pub in_progress: usize,
    pub total: usize,
}

#[derive(Debug, Clone)]
pub struct DailyMatchCount {
    pub date: String,      // YYYY-MM-DD format
    pub count: usize,
}

#[derive(Debug, Clone)]
pub struct ServerFailure {
    pub timestamp: u64,
    pub wall_clock_time: u64,
    pub failure_type: FailureType,
    pub description: String,
    pub match_id: Option<Uuid>,
}

#[derive(Debug, Clone)]
pub struct ActiveMatchInfo {
    pub match_id: Uuid,
    pub game_mode: GameMode,
    pub created_time: u64,   // Unix timestamp when match was created
    pub start_time: Option<u64>,  // Unix timestamp when all players ready (None if still waiting)
    pub participant_countries: Vec<String>,
    pub participant_usernames: Vec<String>,
    pub participant_cities: Vec<String>,
}

pub struct MetricsCollector {
    start_time: Instant,
    server_start_time: u64,    // Unix timestamp when server started
    queue_history: RwLock<VecDeque<QueueSnapshot>>,
    match_events: RwLock<VecDeque<MatchEvent>>,
    latest_server_load: RwLock<ServerLoadMetrics>,
    matches_in_progress: RwLock<HashMap<GameMode, usize>>,
    total_matches_created: RwLock<usize>,
    daily_match_counts: RwLock<VecDeque<DailyMatchCount>>,
    current_day: RwLock<String>,   // Current day in YYYY-MM-DD format
    current_day_count: RwLock<usize>,
    match_start_times: RwLock<HashMap<Uuid, u64>>,  // match_id -> wall_clock_time when started
    active_matches: RwLock<HashMap<Uuid, ActiveMatchInfo>>,  // Currently active matches
    average_match_duration_ms: RwLock<Option<u64>>,
    total_completed_matches: RwLock<usize>,
    total_match_duration_ms: RwLock<u64>,
    failures: RwLock<VecDeque<ServerFailure>>,
    failure_counts: RwLock<HashMap<String, usize>>,
}

impl MetricsCollector {
    pub fn new() -> Arc<Self> {
        let now = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .unwrap()
            .as_secs();
        let today = Utc::now().format("%Y-%m-%d").to_string();

        Arc::new(Self {
            start_time: Instant::now(),
            server_start_time: now,
            queue_history: RwLock::new(VecDeque::with_capacity(QUEUE_HISTORY_SIZE)),
            match_events: RwLock::new(VecDeque::with_capacity(MATCH_EVENT_HISTORY_SIZE)),
            latest_server_load: RwLock::new(ServerLoadMetrics {
                active_connections: 0,
                players_in_queue: 0,
                players_in_match: 0,
                active_sessions: 0,
            }),
            matches_in_progress: RwLock::new(HashMap::new()),
            total_matches_created: RwLock::new(0),
            daily_match_counts: RwLock::new(VecDeque::with_capacity(DAILY_MATCH_HISTORY_SIZE)),
            current_day: RwLock::new(today),
            current_day_count: RwLock::new(0),
            match_start_times: RwLock::new(HashMap::new()),
            active_matches: RwLock::new(HashMap::new()),
            average_match_duration_ms: RwLock::new(None),
            total_completed_matches: RwLock::new(0),
            total_match_duration_ms: RwLock::new(0),
            failures: RwLock::new(VecDeque::with_capacity(FAILURE_HISTORY_SIZE)),
            failure_counts: RwLock::new(HashMap::new()),
        })
    }

    fn current_timestamp(&self) -> u64 {
        self.start_time.elapsed().as_millis() as u64
    }

    pub fn current_wall_clock_time(&self) -> u64 {
        SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .unwrap()
            .as_secs()
    }

    pub fn get_server_start_time(&self) -> u64 {
        self.server_start_time
    }

    async fn check_day_rollover(&self) {
        let today = Utc::now().format("%Y-%m-%d").to_string();
        let mut current_day = self.current_day.write().await;

        if *current_day != today {
            // Day has changed, save current day's count and reset
            let count = {
                let c = self.current_day_count.read().await;
                *c
            };

            if count > 0 || !current_day.is_empty() {
                let mut daily_counts = self.daily_match_counts.write().await;
                daily_counts.push_back(DailyMatchCount {
                    date: current_day.clone(),
                    count,
                });

                // Keep only last 7 days
                while daily_counts.len() > DAILY_MATCH_HISTORY_SIZE {
                    daily_counts.pop_front();
                }
            }

            *current_day = today;
            let mut day_count = self.current_day_count.write().await;
            *day_count = 0;
        }
    }

    pub async fn record_queue_snapshot(
        &self,
        queue_sizes: HashMap<GameMode, usize>,
        longest_wait_ms: HashMap<GameMode, u64>,
    ) {
        let snapshot = QueueSnapshot {
            timestamp: self.current_timestamp(),
            queue_sizes,
            longest_wait_ms,
        };

        let mut history = self.queue_history.write().await;
        if history.len() >= QUEUE_HISTORY_SIZE {
            history.pop_front();
        }
        history.push_back(snapshot);
    }

    pub async fn record_match_event(
        &self,
        game_mode: GameMode,
        event_type: MatchEventType,
        match_id: Option<Uuid>,
        participant_countries: Vec<String>,
        participant_usernames: Vec<String>,
        participant_cities: Vec<String>,
    ) {
        self.check_day_rollover().await;

        let wall_clock = self.current_wall_clock_time();
        let mut duration_ms = None;
        let result = None;

        // Update matches in progress count and track total created
        {
            let mut in_progress = self.matches_in_progress.write().await;
            let count = in_progress.entry(game_mode).or_insert(0);

            match event_type {
                MatchEventType::Created => {
                    *count += 1;

                    // Increment total matches created
                    let mut total = self.total_matches_created.write().await;
                    *total += 1;

                    // Increment current day count
                    let mut day_count = self.current_day_count.write().await;
                    *day_count += 1;

                    // Add to active matches
                    if let Some(mid) = match_id {
                        let mut active = self.active_matches.write().await;
                        active.insert(mid, ActiveMatchInfo {
                            match_id: mid,
                            game_mode,
                            created_time: wall_clock,
                            start_time: None,
                            participant_countries: participant_countries.clone(),
                            participant_usernames: participant_usernames.clone(),
                            participant_cities: participant_cities.clone(),
                        });
                    }
                }
                MatchEventType::Started => {
                    // Record match start time for duration tracking
                    if let Some(mid) = match_id {
                        let mut start_times = self.match_start_times.write().await;
                        start_times.insert(mid, wall_clock);

                        // Update active match with start time
                        let mut active = self.active_matches.write().await;
                        if let Some(info) = active.get_mut(&mid) {
                            info.start_time = Some(wall_clock);
                        }
                    }
                }
                MatchEventType::Completed => {
                    if *count > 0 {
                        *count -= 1;
                    }

                    // Remove from active matches
                    if let Some(mid) = match_id {
                        let mut active = self.active_matches.write().await;
                        active.remove(&mid);
                    }

                    // Calculate duration if we have start time
                    if let Some(mid) = match_id {
                        let mut start_times = self.match_start_times.write().await;
                        if let Some(start_time) = start_times.remove(&mid) {
                            let dur = (wall_clock - start_time) * 1000; // Convert to ms
                            duration_ms = Some(dur);

                            // Update average duration
                            let mut total_dur = self.total_match_duration_ms.write().await;
                            let mut total_completed = self.total_completed_matches.write().await;
                            *total_dur += dur;
                            *total_completed += 1;

                            let mut avg = self.average_match_duration_ms.write().await;
                            *avg = Some(*total_dur / *total_completed as u64);
                        }
                    }
                }
            }
        }

        let event = MatchEvent {
            timestamp: self.current_timestamp(),
            wall_clock_time: wall_clock,
            game_mode,
            event_type,
            match_id,
            duration_ms,
            result,
            participant_countries,
            participant_usernames,
            participant_cities,
        };

        let mut events = self.match_events.write().await;
        if events.len() >= MATCH_EVENT_HISTORY_SIZE {
            events.pop_front();
        }
        events.push_back(event);
    }

    /// Records a match completion with result.
    /// Returns the duration in milliseconds if available.
    pub async fn record_match_completed_with_result(
        &self,
        game_mode: GameMode,
        match_id: Uuid,
        result: MatchResult,
        participant_countries: Vec<String>,
        participant_usernames: Vec<String>,
        participant_cities: Vec<String>,
    ) -> Option<u64> {
        let wall_clock = self.current_wall_clock_time();
        let mut duration_ms = None;

        // Update matches in progress count
        {
            let mut in_progress = self.matches_in_progress.write().await;
            let count = in_progress.entry(game_mode).or_insert(0);
            if *count > 0 {
                *count -= 1;
            }

            // Remove from active matches
            let mut active = self.active_matches.write().await;
            active.remove(&match_id);

            // Calculate duration
            let mut start_times = self.match_start_times.write().await;
            if let Some(start_time) = start_times.remove(&match_id) {
                let dur = (wall_clock - start_time) * 1000;
                duration_ms = Some(dur);

                let mut total_dur = self.total_match_duration_ms.write().await;
                let mut total_completed = self.total_completed_matches.write().await;
                *total_dur += dur;
                *total_completed += 1;

                let mut avg = self.average_match_duration_ms.write().await;
                *avg = Some(*total_dur / *total_completed as u64);
            }
        }

        let event = MatchEvent {
            timestamp: self.current_timestamp(),
            wall_clock_time: wall_clock,
            game_mode,
            event_type: MatchEventType::Completed,
            match_id: Some(match_id),
            duration_ms,
            result: Some(result),
            participant_countries,
            participant_usernames,
            participant_cities,
        };

        let mut events = self.match_events.write().await;
        if events.len() >= MATCH_EVENT_HISTORY_SIZE {
            events.pop_front();
        }
        events.push_back(event);

        duration_ms
    }

    pub async fn record_match_started(&self, match_id: Uuid) {
        let wall_clock = self.current_wall_clock_time();
        let mut start_times = self.match_start_times.write().await;
        start_times.insert(match_id, wall_clock);

        // Update active match with start time
        let mut active = self.active_matches.write().await;
        if let Some(info) = active.get_mut(&match_id) {
            info.start_time = Some(wall_clock);
        }
    }

    pub async fn get_active_matches(&self) -> Vec<ActiveMatchInfo> {
        let active = self.active_matches.read().await;
        active.values().cloned().collect()
    }

    /// Record a match as abandoned (all players disconnected)
    /// Returns the duration in milliseconds if available
    pub async fn record_match_abandoned(
        &self,
        game_mode: GameMode,
        match_id: Uuid,
        participant_countries: Vec<String>,
        participant_usernames: Vec<String>,
        participant_cities: Vec<String>,
    ) -> Option<u64> {
        let wall_clock = self.current_wall_clock_time();
        let mut duration_ms = None;

        // Remove from active matches
        {
            let mut active = self.active_matches.write().await;
            active.remove(&match_id);
        }

        // Update matches in progress count
        {
            let mut in_progress = self.matches_in_progress.write().await;
            let count = in_progress.entry(game_mode).or_insert(0);
            if *count > 0 {
                *count -= 1;
            }
        }

        // Calculate duration before removing from match start times
        {
            let mut start_times = self.match_start_times.write().await;
            if let Some(start_time) = start_times.remove(&match_id) {
                let dur = (wall_clock - start_time) * 1000; // Convert to ms
                duration_ms = Some(dur);

                // Update average duration
                let mut total_dur = self.total_match_duration_ms.write().await;
                let mut total_completed = self.total_completed_matches.write().await;
                *total_dur += dur;
                *total_completed += 1;

                let mut avg = self.average_match_duration_ms.write().await;
                *avg = Some(*total_dur / *total_completed as u64);
            }
        }

        // Record as a completion event with Abandoned result
        let event = MatchEvent {
            timestamp: self.current_timestamp(),
            wall_clock_time: wall_clock,
            game_mode,
            event_type: MatchEventType::Completed,
            match_id: Some(match_id),
            duration_ms,
            result: Some(MatchResult::Abandoned),
            participant_countries,
            participant_usernames,
            participant_cities,
        };

        let mut events = self.match_events.write().await;
        if events.len() >= MATCH_EVENT_HISTORY_SIZE {
            events.pop_front();
        }
        events.push_back(event);

        // Also record as a failure event for the server health tracking
        let failure = ServerFailure {
            timestamp: self.current_timestamp(),
            wall_clock_time: wall_clock,
            failure_type: FailureType::PlayerDisconnect,
            description: "Match abandoned - all players disconnected".to_string(),
            match_id: Some(match_id),
        };

        // Update failure counts
        {
            let mut counts = self.failure_counts.write().await;
            let key = FailureType::PlayerDisconnect.to_string();
            *counts.entry(key).or_insert(0) += 1;
        }

        let mut failures = self.failures.write().await;
        if failures.len() >= FAILURE_HISTORY_SIZE {
            failures.pop_front();
        }
        failures.push_back(failure);

        duration_ms
    }

    pub async fn record_failure(
        &self,
        failure_type: FailureType,
        description: String,
        match_id: Option<Uuid>,
    ) {
        let wall_clock = self.current_wall_clock_time();

        let failure = ServerFailure {
            timestamp: self.current_timestamp(),
            wall_clock_time: wall_clock,
            failure_type,
            description,
            match_id,
        };

        // Update failure counts
        {
            let mut counts = self.failure_counts.write().await;
            let key = failure_type.to_string();
            *counts.entry(key).or_insert(0) += 1;
        }

        let mut failures = self.failures.write().await;
        if failures.len() >= FAILURE_HISTORY_SIZE {
            failures.pop_front();
        }
        failures.push_back(failure);
    }

    pub async fn update_server_load(&self, load: ServerLoadMetrics) {
        let mut latest = self.latest_server_load.write().await;
        *latest = load;
    }

    pub async fn get_queue_history(&self) -> Vec<QueueSnapshot> {
        let history = self.queue_history.read().await;
        history.iter().cloned().collect()
    }

    pub async fn get_recent_match_events(&self, limit: usize) -> Vec<MatchEvent> {
        let events = self.match_events.read().await;
        events.iter().rev().take(limit).cloned().collect()
    }

    pub async fn get_server_load(&self) -> ServerLoadMetrics {
        self.latest_server_load.read().await.clone()
    }

    pub async fn get_matches_in_progress(&self) -> HashMap<GameMode, usize> {
        self.matches_in_progress.read().await.clone()
    }

    pub async fn get_total_matches_created(&self) -> usize {
        *self.total_matches_created.read().await
    }

    pub async fn get_daily_match_counts(&self) -> Vec<DailyMatchCount> {
        self.check_day_rollover().await;

        let mut result: Vec<DailyMatchCount> = {
            let counts = self.daily_match_counts.read().await;
            counts.iter().cloned().collect()
        };

        // Add current day
        let current_day = self.current_day.read().await.clone();
        let current_count = *self.current_day_count.read().await;

        result.push(DailyMatchCount {
            date: current_day,
            count: current_count,
        });

        result
    }

    pub async fn get_average_match_duration_ms(&self) -> Option<u64> {
        *self.average_match_duration_ms.read().await
    }

    pub async fn get_recent_failures(&self, limit: usize) -> Vec<ServerFailure> {
        let failures = self.failures.read().await;
        failures.iter().rev().take(limit).cloned().collect()
    }

    pub async fn get_failure_counts(&self) -> HashMap<String, usize> {
        self.failure_counts.read().await.clone()
    }

    pub async fn get_matches_created_last_hour(&self) -> usize {
        let one_hour_ms = 60 * 60 * 1000;
        let cutoff = self.current_timestamp().saturating_sub(one_hour_ms);

        let events = self.match_events.read().await;
        events
            .iter()
            .filter(|e| e.timestamp >= cutoff && e.event_type == MatchEventType::Created)
            .count()
    }

    pub async fn get_matches_per_minute_history(&self) -> Vec<(u64, usize)> {
        let thirty_minutes_ms: u64 = 30 * 60 * 1000;
        let minute_ms: u64 = 60 * 1000;
        let now = self.current_timestamp();
        let cutoff = now.saturating_sub(thirty_minutes_ms);

        let events = self.match_events.read().await;

        // Group events by minute (using wall clock time for proper frontend display)
        let mut minutes: HashMap<u64, usize> = HashMap::new();
        for event in events.iter() {
            if event.timestamp >= cutoff && event.event_type == MatchEventType::Created {
                // Convert relative timestamp to absolute Unix ms, then bucket by minute
                let absolute_ms = self.server_start_time * 1000 + event.timestamp;
                let minute_bucket = absolute_ms / minute_ms;
                *minutes.entry(minute_bucket).or_insert(0) += 1;
            }
        }

        // Convert to sorted list
        let mut result: Vec<_> = minutes.into_iter().collect();
        result.sort_by_key(|(ts, _)| *ts);
        result
    }

    pub async fn get_latest_queue_snapshot(&self) -> Option<QueueSnapshot> {
        let history = self.queue_history.read().await;
        history.back().cloned()
    }

    /// Load persisted metrics from the database on startup
    pub async fn load_from_db(&self, db: &MetricsDb) {
        // Load total matches created
        match db.get_total_matches_created().await {
            Ok(count) => {
                let mut total = self.total_matches_created.write().await;
                *total = count;
                tracing::info!("Loaded total_matches_created from DB: {}", count);
            }
            Err(e) => {
                tracing::error!("Failed to load total_matches_created from DB: {}", e);
            }
        }

        // Load total completed matches
        let completed_count = match db.get_total_matches_completed().await {
            Ok(count) => {
                let mut total = self.total_completed_matches.write().await;
                *total = count;
                tracing::info!("Loaded total_completed_matches from DB: {}", count);
                count
            }
            Err(e) => {
                tracing::error!("Failed to load total_completed_matches from DB: {}", e);
                0
            }
        };

        // Load total match duration and calculate average
        match db.get_total_match_duration_ms().await {
            Ok(total_duration) => {
                let mut total_dur = self.total_match_duration_ms.write().await;
                *total_dur = total_duration;

                // Calculate average if we have completed matches
                if completed_count > 0 {
                    let mut avg = self.average_match_duration_ms.write().await;
                    *avg = Some(total_duration / completed_count as u64);
                    tracing::info!(
                        "Loaded total_match_duration_ms: {}, average: {}",
                        total_duration,
                        total_duration / completed_count as u64
                    );
                }
            }
            Err(e) => {
                tracing::error!("Failed to load total_match_duration_ms from DB: {}", e);
            }
        }

        // Load today's match count
        let today = Utc::now().format("%Y-%m-%d").to_string();
        match db.get_todays_match_count(&today).await {
            Ok(count) => {
                let mut day_count = self.current_day_count.write().await;
                *day_count = count;
                tracing::info!("Loaded today's match count from DB: {}", count);
            }
            Err(e) => {
                tracing::error!("Failed to load today's match count from DB: {}", e);
            }
        }

        // Load failure counts
        match db.get_failure_counts().await {
            Ok(counts) => {
                let mut failure_counts = self.failure_counts.write().await;
                *failure_counts = counts.clone();
                tracing::info!("Loaded failure counts from DB: {:?}", counts);
            }
            Err(e) => {
                tracing::error!("Failed to load failure counts from DB: {}", e);
            }
        }

        // Load recent match events into in-memory history
        match db.get_match_history(7).await {
            Ok(records) => {
                let mut events = self.match_events.write().await;
                let mut loaded_count = 0;

                // Records are returned in DESC order, so reverse to get chronological order
                for record in records.into_iter().rev() {
                    // Parse event type
                    let event_type = match record.event_type.as_str() {
                        "created" => MatchEventType::Created,
                        "started" => MatchEventType::Started,
                        "completed" => MatchEventType::Completed,
                        _ => continue,
                    };

                    // Parse game mode
                    let game_mode = match record.game_mode.as_str() {
                        "1v1" => GameMode::OneVsOne,
                        "2v2" => GameMode::TwoVsTwo,
                        "3v3" => GameMode::ThreeVsThree,
                        "4v4" => GameMode::FourVsFour,
                        "single_player" => GameMode::SinglePlayer,
                        _ => continue,
                    };

                    // Parse match_id
                    let match_id = Uuid::parse_str(&record.match_id).ok();

                    // Parse result
                    let result = match record.result.as_deref() {
                        Some("victory") => record.winning_team.map(|t| MatchResult::Victory {
                            winning_team: t as u8,
                        }),
                        Some("surrender") => Some(MatchResult::Surrender),
                        Some("draw") => Some(MatchResult::Draw),
                        Some("abandoned") => Some(MatchResult::Abandoned),
                        _ => None,
                    };

                    // Parse participant countries
                    let participant_countries: Vec<String> =
                        serde_json::from_str(&record.participant_countries).unwrap_or_default();

                    // Parse participant usernames
                    let participant_usernames: Vec<String> = record
                        .participant_usernames
                        .as_ref()
                        .and_then(|s| serde_json::from_str(s).ok())
                        .unwrap_or_default();

                    // Parse participant cities
                    let participant_cities: Vec<String> = record
                        .participant_cities
                        .as_ref()
                        .and_then(|s| serde_json::from_str(s).ok())
                        .unwrap_or_default();

                    let event = MatchEvent {
                        timestamp: 0, // Relative timestamp not meaningful for loaded events
                        wall_clock_time: record.timestamp as u64,
                        game_mode,
                        event_type,
                        match_id,
                        duration_ms: record.duration_ms.map(|d| d as u64),
                        result,
                        participant_countries,
                        participant_usernames,
                        participant_cities,
                    };

                    if events.len() >= MATCH_EVENT_HISTORY_SIZE {
                        events.pop_front();
                    }
                    events.push_back(event);
                    loaded_count += 1;
                }

                tracing::info!("Loaded {} recent match events from DB", loaded_count);
            }
            Err(e) => {
                tracing::error!("Failed to load match events from DB: {}", e);
            }
        }
    }
}

impl Default for MetricsCollector {
    fn default() -> Self {
        let now = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .unwrap()
            .as_secs();
        let today = Utc::now().format("%Y-%m-%d").to_string();

        Self {
            start_time: Instant::now(),
            server_start_time: now,
            queue_history: RwLock::new(VecDeque::with_capacity(QUEUE_HISTORY_SIZE)),
            match_events: RwLock::new(VecDeque::with_capacity(MATCH_EVENT_HISTORY_SIZE)),
            latest_server_load: RwLock::new(ServerLoadMetrics {
                active_connections: 0,
                players_in_queue: 0,
                players_in_match: 0,
                active_sessions: 0,
            }),
            matches_in_progress: RwLock::new(HashMap::new()),
            total_matches_created: RwLock::new(0),
            daily_match_counts: RwLock::new(VecDeque::with_capacity(DAILY_MATCH_HISTORY_SIZE)),
            current_day: RwLock::new(today),
            current_day_count: RwLock::new(0),
            match_start_times: RwLock::new(HashMap::new()),
            active_matches: RwLock::new(HashMap::new()),
            average_match_duration_ms: RwLock::new(None),
            total_completed_matches: RwLock::new(0),
            total_match_duration_ms: RwLock::new(0),
            failures: RwLock::new(VecDeque::with_capacity(FAILURE_HISTORY_SIZE)),
            failure_counts: RwLock::new(HashMap::new()),
        }
    }
}
