pub struct Config {
    pub server_port: u16,
    pub matchmaking_interval_ms: u64,
}

impl Default for Config {
    fn default() -> Self {
        Self {
            server_port: 8081,
            matchmaking_interval_ms: 1000,
        }
    }
}

impl Config {
    pub fn from_env() -> Self {
        Self {
            server_port: std::env::var("PORT")
                .ok()
                .and_then(|p| p.parse().ok())
                .unwrap_or(8081),
            matchmaking_interval_ms: std::env::var("MATCHMAKING_INTERVAL_MS")
                .ok()
                .and_then(|p| p.parse().ok())
                .unwrap_or(1000),
        }
    }
}
