use sqlx::{PgPool, Row};
use uuid::Uuid;

use crate::models::GameMode;
use crate::services::metrics::{MatchEventType, MatchResult};

pub struct MetricsDb {
    pool: PgPool,
}

#[derive(Debug, Clone)]
pub struct MatchEventRecord {
    pub id: i64,
    pub match_id: String,
    pub event_type: String,
    pub game_mode: String,
    pub player_count: Option<i32>,
    pub duration_ms: Option<i64>,
    pub result: Option<String>,
    pub winning_team: Option<i32>,
    pub participant_countries: String,
    pub participant_usernames: Option<String>,
    pub participant_cities: Option<String>,
    pub timestamp: i64,
}

#[derive(Debug, Clone)]
#[allow(dead_code)]
pub struct QueueSnapshotRecord {
    pub id: i64,
    pub timestamp: i64,
    pub mode_1v1: i32,
    pub mode_2v2: i32,
    pub mode_3v3: i32,
    pub mode_4v4: i32,
    pub longest_wait_1v1: Option<i64>,
    pub longest_wait_2v2: Option<i64>,
    pub longest_wait_3v3: Option<i64>,
    pub longest_wait_4v4: Option<i64>,
}

#[derive(Debug, Clone)]
pub struct ServerFailureRecord {
    pub id: i64,
    pub timestamp: i64,
    pub failure_type: String,
    pub description: Option<String>,
    pub match_id: Option<String>,
}

#[derive(Debug, Clone)]
pub struct DailyStatsRecord {
    pub date: String,
    pub matches_created: i32,
    pub matches_completed: i32,
    pub avg_match_duration_ms: Option<i64>,
    pub avg_queue_wait_ms: Option<i64>,
}

impl MetricsDb {
    pub async fn new(database_url: &str) -> Result<Self, sqlx::Error> {
        let pool = PgPool::connect(database_url).await?;
        let db = Self { pool };
        db.initialize_schema().await?;
        Ok(db)
    }

    async fn initialize_schema(&self) -> Result<(), sqlx::Error> {
        sqlx::query(
            r#"
            CREATE TABLE IF NOT EXISTS match_events (
                id BIGSERIAL PRIMARY KEY,
                match_id TEXT NOT NULL,
                event_type TEXT NOT NULL,
                game_mode TEXT NOT NULL,
                player_count INTEGER,
                duration_ms BIGINT,
                result TEXT,
                winning_team INTEGER,
                participant_countries TEXT,
                timestamp BIGINT NOT NULL
            )
            "#,
        )
        .execute(&self.pool)
        .await?;

        sqlx::query(
            r#"
            CREATE TABLE IF NOT EXISTS queue_snapshots (
                id BIGSERIAL PRIMARY KEY,
                timestamp BIGINT NOT NULL,
                mode_1v1 INTEGER NOT NULL,
                mode_2v2 INTEGER NOT NULL,
                mode_3v3 INTEGER NOT NULL,
                mode_4v4 INTEGER NOT NULL,
                longest_wait_1v1 BIGINT,
                longest_wait_2v2 BIGINT,
                longest_wait_3v3 BIGINT,
                longest_wait_4v4 BIGINT
            )
            "#,
        )
        .execute(&self.pool)
        .await?;

        sqlx::query(
            r#"
            CREATE TABLE IF NOT EXISTS server_failures (
                id BIGSERIAL PRIMARY KEY,
                timestamp BIGINT NOT NULL,
                failure_type TEXT NOT NULL,
                description TEXT,
                match_id TEXT
            )
            "#,
        )
        .execute(&self.pool)
        .await?;

        sqlx::query(
            r#"
            CREATE TABLE IF NOT EXISTS daily_stats (
                date TEXT PRIMARY KEY,
                matches_created INTEGER,
                matches_completed INTEGER,
                avg_match_duration_ms BIGINT,
                avg_queue_wait_ms BIGINT
            )
            "#,
        )
        .execute(&self.pool)
        .await?;

        sqlx::query(
            "CREATE INDEX IF NOT EXISTS idx_match_events_timestamp ON match_events(timestamp)",
        )
        .execute(&self.pool)
        .await?;

        sqlx::query(
            "CREATE INDEX IF NOT EXISTS idx_queue_snapshots_timestamp ON queue_snapshots(timestamp)",
        )
        .execute(&self.pool)
        .await?;

        // Add columns for usernames and cities if they don't exist (migration)
        sqlx::query("ALTER TABLE match_events ADD COLUMN IF NOT EXISTS participant_usernames TEXT")
            .execute(&self.pool)
            .await
            .ok();
        sqlx::query("ALTER TABLE match_events ADD COLUMN IF NOT EXISTS participant_cities TEXT")
            .execute(&self.pool)
            .await
            .ok();

        Ok(())
    }

    pub async fn insert_match_event(
        &self,
        match_id: Uuid,
        event_type: MatchEventType,
        game_mode: GameMode,
        player_count: Option<i32>,
        duration_ms: Option<i64>,
        result: Option<&MatchResult>,
        participant_countries: &[String],
        participant_usernames: &[String],
        participant_cities: &[String],
        timestamp: i64,
    ) -> Result<(), sqlx::Error> {
        let event_type_str = match event_type {
            MatchEventType::Created => "created",
            MatchEventType::Started => "started",
            MatchEventType::Completed => "completed",
        };

        let game_mode_str = game_mode_to_str(game_mode);

        let (result_str, winning_team): (Option<&str>, Option<i32>) = match result {
            Some(MatchResult::Victory { winning_team }) => {
                (Some("victory"), Some(*winning_team as i32))
            }
            Some(MatchResult::Surrender) => (Some("surrender"), None),
            Some(MatchResult::Draw) => (Some("draw"), None),
            Some(MatchResult::Abandoned) => (Some("abandoned"), None),
            None => (None, None),
        };

        let countries_json =
            serde_json::to_string(participant_countries).unwrap_or_else(|_| "[]".to_string());
        let usernames_json =
            serde_json::to_string(participant_usernames).unwrap_or_else(|_| "[]".to_string());
        let cities_json =
            serde_json::to_string(participant_cities).unwrap_or_else(|_| "[]".to_string());

        sqlx::query(
            r#"INSERT INTO match_events
               (match_id, event_type, game_mode, player_count, duration_ms, result, winning_team, participant_countries, participant_usernames, participant_cities, timestamp)
               VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11)"#,
        )
        .bind(match_id.to_string())
        .bind(event_type_str)
        .bind(game_mode_str)
        .bind(player_count)
        .bind(duration_ms)
        .bind(result_str)
        .bind(winning_team)
        .bind(countries_json)
        .bind(usernames_json)
        .bind(cities_json)
        .bind(timestamp)
        .execute(&self.pool)
        .await?;

        Ok(())
    }

    pub async fn insert_queue_snapshot(
        &self,
        timestamp: i64,
        mode_1v1: i32,
        mode_2v2: i32,
        mode_3v3: i32,
        mode_4v4: i32,
        longest_wait_1v1: Option<i64>,
        longest_wait_2v2: Option<i64>,
        longest_wait_3v3: Option<i64>,
        longest_wait_4v4: Option<i64>,
    ) -> Result<(), sqlx::Error> {
        sqlx::query(
            r#"INSERT INTO queue_snapshots
               (timestamp, mode_1v1, mode_2v2, mode_3v3, mode_4v4, longest_wait_1v1, longest_wait_2v2, longest_wait_3v3, longest_wait_4v4)
               VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9)"#,
        )
        .bind(timestamp)
        .bind(mode_1v1)
        .bind(mode_2v2)
        .bind(mode_3v3)
        .bind(mode_4v4)
        .bind(longest_wait_1v1)
        .bind(longest_wait_2v2)
        .bind(longest_wait_3v3)
        .bind(longest_wait_4v4)
        .execute(&self.pool)
        .await?;

        Ok(())
    }

    pub async fn insert_failure(
        &self,
        timestamp: i64,
        failure_type: &str,
        description: Option<&str>,
        match_id: Option<Uuid>,
    ) -> Result<(), sqlx::Error> {
        sqlx::query(
            r#"INSERT INTO server_failures (timestamp, failure_type, description, match_id)
               VALUES ($1, $2, $3, $4)"#,
        )
        .bind(timestamp)
        .bind(failure_type)
        .bind(description)
        .bind(match_id.map(|id| id.to_string()))
        .execute(&self.pool)
        .await?;

        Ok(())
    }

    pub async fn get_match_history(&self, days: i64) -> Result<Vec<MatchEventRecord>, sqlx::Error> {
        let cutoff = chrono::Utc::now().timestamp() - (days * 24 * 60 * 60);

        let rows = sqlx::query(
            r#"SELECT id, match_id, event_type, game_mode, player_count, duration_ms,
                      result, winning_team, participant_countries, participant_usernames, participant_cities, timestamp
               FROM match_events
               WHERE timestamp >= $1
               ORDER BY timestamp DESC"#,
        )
        .bind(cutoff)
        .fetch_all(&self.pool)
        .await?;

        let records = rows
            .into_iter()
            .map(|row| MatchEventRecord {
                id: row.get("id"),
                match_id: row.get("match_id"),
                event_type: row.get("event_type"),
                game_mode: row.get("game_mode"),
                player_count: row.get("player_count"),
                duration_ms: row.get("duration_ms"),
                result: row.get("result"),
                winning_team: row.get("winning_team"),
                participant_countries: row.get("participant_countries"),
                participant_usernames: row.get("participant_usernames"),
                participant_cities: row.get("participant_cities"),
                timestamp: row.get("timestamp"),
            })
            .collect();

        Ok(records)
    }

    pub async fn get_recent_match_events(
        &self,
        limit: i64,
    ) -> Result<Vec<MatchEventRecord>, sqlx::Error> {
        let rows = sqlx::query(
            r#"SELECT id, match_id, event_type, game_mode, player_count, duration_ms,
                      result, winning_team, participant_countries, participant_usernames, participant_cities, timestamp
               FROM match_events
               ORDER BY timestamp DESC
               LIMIT $1"#,
        )
        .bind(limit)
        .fetch_all(&self.pool)
        .await?;

        let records = rows
            .into_iter()
            .map(|row| MatchEventRecord {
                id: row.get("id"),
                match_id: row.get("match_id"),
                event_type: row.get("event_type"),
                game_mode: row.get("game_mode"),
                player_count: row.get("player_count"),
                duration_ms: row.get("duration_ms"),
                result: row.get("result"),
                winning_team: row.get("winning_team"),
                participant_countries: row.get("participant_countries"),
                participant_usernames: row.get("participant_usernames"),
                participant_cities: row.get("participant_cities"),
                timestamp: row.get("timestamp"),
            })
            .collect();

        Ok(records)
    }

    pub async fn get_daily_stats(&self) -> Result<Vec<DailyStatsRecord>, sqlx::Error> {
        let rows = sqlx::query(
            r#"SELECT date, matches_created, matches_completed, avg_match_duration_ms, avg_queue_wait_ms
               FROM daily_stats
               ORDER BY date DESC
               LIMIT 30"#,
        )
        .fetch_all(&self.pool)
        .await?;

        let records = rows
            .into_iter()
            .map(|row| DailyStatsRecord {
                date: row.get("date"),
                matches_created: row.get("matches_created"),
                matches_completed: row.get("matches_completed"),
                avg_match_duration_ms: row.get("avg_match_duration_ms"),
                avg_queue_wait_ms: row.get("avg_queue_wait_ms"),
            })
            .collect();

        Ok(records)
    }

    pub async fn update_daily_stats(&self, date: &str) -> Result<(), sqlx::Error> {
        let date_parsed = chrono::NaiveDate::parse_from_str(date, "%Y-%m-%d")
            .map_err(|e| sqlx::Error::Protocol(format!("Invalid date format: {}", e)))?;
        let start_of_day = date_parsed
            .and_hms_opt(0, 0, 0)
            .unwrap()
            .and_utc()
            .timestamp();
        let end_of_day = date_parsed
            .and_hms_opt(23, 59, 59)
            .unwrap()
            .and_utc()
            .timestamp();

        let matches_created: i64 = sqlx::query_scalar(
            r#"SELECT COUNT(*) FROM match_events
               WHERE timestamp >= $1 AND timestamp <= $2 AND event_type = 'created'"#,
        )
        .bind(start_of_day)
        .bind(end_of_day)
        .fetch_one(&self.pool)
        .await?;

        let matches_completed: i64 = sqlx::query_scalar(
            r#"SELECT COUNT(*) FROM match_events
               WHERE timestamp >= $1 AND timestamp <= $2 AND event_type = 'completed'"#,
        )
        .bind(start_of_day)
        .bind(end_of_day)
        .fetch_one(&self.pool)
        .await?;

        let avg_duration: Option<i64> = sqlx::query_scalar(
            r#"SELECT AVG(duration_ms)::BIGINT FROM match_events
               WHERE timestamp >= $1 AND timestamp <= $2 AND event_type = 'completed' AND duration_ms IS NOT NULL"#,
        )
        .bind(start_of_day)
        .bind(end_of_day)
        .fetch_one(&self.pool)
        .await?;

        sqlx::query(
            r#"INSERT INTO daily_stats (date, matches_created, matches_completed, avg_match_duration_ms, avg_queue_wait_ms)
               VALUES ($1, $2, $3, $4, NULL)
               ON CONFLICT (date) DO UPDATE SET
                   matches_created = EXCLUDED.matches_created,
                   matches_completed = EXCLUDED.matches_completed,
                   avg_match_duration_ms = EXCLUDED.avg_match_duration_ms"#,
        )
        .bind(date)
        .bind(matches_created as i32)
        .bind(matches_completed as i32)
        .bind(avg_duration)
        .execute(&self.pool)
        .await?;

        Ok(())
    }

    pub async fn get_recent_failures(
        &self,
        limit: i64,
    ) -> Result<Vec<ServerFailureRecord>, sqlx::Error> {
        let rows = sqlx::query(
            r#"SELECT id, timestamp, failure_type, description, match_id
               FROM server_failures
               ORDER BY timestamp DESC
               LIMIT $1"#,
        )
        .bind(limit)
        .fetch_all(&self.pool)
        .await?;

        let records = rows
            .into_iter()
            .map(|row| ServerFailureRecord {
                id: row.get("id"),
                timestamp: row.get("timestamp"),
                failure_type: row.get("failure_type"),
                description: row.get("description"),
                match_id: row.get("match_id"),
            })
            .collect();

        Ok(records)
    }
}

fn game_mode_to_str(mode: GameMode) -> &'static str {
    match mode {
        GameMode::OneVsOne => "1v1",
        GameMode::TwoVsTwo => "2v2",
        GameMode::ThreeVsThree => "3v3",
        GameMode::FourVsFour => "4v4",
        GameMode::SinglePlayer => "single_player",
    }
}

// Startup data retrieval methods
impl MetricsDb {
    pub async fn get_total_matches_created(&self) -> Result<usize, sqlx::Error> {
        let count: i64 = sqlx::query_scalar(
            "SELECT COUNT(*) FROM match_events WHERE event_type = 'created'",
        )
        .fetch_one(&self.pool)
        .await?;
        Ok(count as usize)
    }

    pub async fn get_total_matches_completed(&self) -> Result<usize, sqlx::Error> {
        let count: i64 = sqlx::query_scalar(
            "SELECT COUNT(*) FROM match_events WHERE event_type = 'completed'",
        )
        .fetch_one(&self.pool)
        .await?;
        Ok(count as usize)
    }

    pub async fn get_total_match_duration_ms(&self) -> Result<u64, sqlx::Error> {
        let total: Option<i64> = sqlx::query_scalar(
            "SELECT SUM(duration_ms)::BIGINT FROM match_events WHERE event_type = 'completed' AND duration_ms IS NOT NULL",
        )
        .fetch_one(&self.pool)
        .await?;
        Ok(total.unwrap_or(0) as u64)
    }

    pub async fn get_todays_match_count(&self, date: &str) -> Result<usize, sqlx::Error> {
        let date_parsed = chrono::NaiveDate::parse_from_str(date, "%Y-%m-%d")
            .map_err(|e| sqlx::Error::Protocol(format!("Invalid date format: {}", e)))?;
        let start_of_day = date_parsed
            .and_hms_opt(0, 0, 0)
            .unwrap()
            .and_utc()
            .timestamp();
        let end_of_day = date_parsed
            .and_hms_opt(23, 59, 59)
            .unwrap()
            .and_utc()
            .timestamp();

        let count: i64 = sqlx::query_scalar(
            "SELECT COUNT(*) FROM match_events WHERE event_type = 'created' AND timestamp >= $1 AND timestamp <= $2",
        )
        .bind(start_of_day)
        .bind(end_of_day)
        .fetch_one(&self.pool)
        .await?;
        Ok(count as usize)
    }

    pub async fn get_failure_counts(&self) -> Result<std::collections::HashMap<String, usize>, sqlx::Error> {
        let rows = sqlx::query(
            "SELECT failure_type, COUNT(*) as count FROM server_failures GROUP BY failure_type",
        )
        .fetch_all(&self.pool)
        .await?;

        let mut counts = std::collections::HashMap::new();
        for row in rows {
            let failure_type: String = row.get("failure_type");
            let count: i64 = row.get("count");
            counts.insert(failure_type, count as usize);
        }
        Ok(counts)
    }
}
