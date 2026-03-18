use std::net::SocketAddr;
use std::time::Duration;

use axum::{
    extract::{
        ws::{Message, WebSocket, WebSocketUpgrade},
        ConnectInfo, State,
    },
    http::HeaderMap,
    response::IntoResponse,
};
use futures::{SinkExt, StreamExt};
use std::sync::Arc;
use tokio::sync::mpsc;
use uuid::Uuid;

use crate::api::dashboard::broadcast_dashboard_update;
use crate::models::dashboard_messages::{
    game_mode_to_string, match_result_to_string, DashboardMessage, ParticipantLocation,
    RecentMatchEvent,
};
use crate::models::{
    ClientMessage, ConnectionInfo, GameMode, MatchSession, MatchState, PlayerState, QueueEntry,
    ReconnectCommand, ReconnectFrame, ServerMessage, TeamPlayer, MatchResult as ClientMatchResult,
};
use crate::services::metrics::{FailureType, MatchEventType, MatchResult as MetricsMatchResult};
use crate::AppState;

pub async fn ws_handler(
    ws: WebSocketUpgrade,
    headers: HeaderMap,
    ConnectInfo(addr): ConnectInfo<SocketAddr>,
    State(state): State<Arc<AppState>>,
) -> impl IntoResponse {
    // Get real client IP - check X-Forwarded-For first (for reverse proxies like Render)
    // then fall back to direct connection IP
    let client_ip = get_real_client_ip(&headers, &addr);
    ws.on_upgrade(move |socket| handle_socket(socket, state, client_ip))
}

fn get_real_client_ip(headers: &HeaderMap, addr: &SocketAddr) -> String {
    // Check X-Forwarded-For header first (comma-separated list, first is original client)
    if let Some(forwarded_for) = headers.get("x-forwarded-for") {
        if let Ok(value) = forwarded_for.to_str() {
            // Take the first IP in the list (original client)
            if let Some(first_ip) = value.split(',').next() {
                let ip = first_ip.trim();
                if !ip.is_empty() {
                    return ip.to_string();
                }
            }
        }
    }

    // Check X-Real-IP header (single IP, set by some proxies)
    if let Some(real_ip) = headers.get("x-real-ip") {
        if let Ok(value) = real_ip.to_str() {
            let ip = value.trim();
            if !ip.is_empty() {
                return ip.to_string();
            }
        }
    }

    // Check CF-Connecting-IP header (Cloudflare)
    if let Some(cf_ip) = headers.get("cf-connecting-ip") {
        if let Ok(value) = cf_ip.to_str() {
            let ip = value.trim();
            if !ip.is_empty() {
                return ip.to_string();
            }
        }
    }

    // Fall back to direct connection IP
    addr.ip().to_string()
}

async fn handle_socket(socket: WebSocket, state: Arc<AppState>, client_ip: String) {
    let (mut sender, mut receiver) = socket.split();
    let (tx, mut rx) = mpsc::unbounded_channel::<ServerMessage>();

    let mut player_token: Option<String> = None;
    let mut player_id: Option<Uuid> = None;

    let send_task = tokio::spawn(async move {
        while let Some(msg) = rx.recv().await {
            if let Ok(json) = serde_json::to_string(&msg) {
                if sender.send(Message::Text(json.into())).await.is_err() {
                    break;
                }
            }
        }
    });

    let state_clone = state.clone();
    let tx_clone = tx.clone();
    let client_ip_clone = client_ip.clone();

    while let Some(msg) = receiver.next().await {
        let msg = match msg {
            Ok(Message::Text(text)) => text,
            Ok(Message::Close(_)) => break,
            Err(_) => break,
            _ => continue,
        };

        let client_msg: ClientMessage = match serde_json::from_str(&msg) {
            Ok(m) => m,
            Err(e) => {
                let _ = tx.send(ServerMessage::Error {
                    message: format!("Invalid message: {}", e),
                });
                continue;
            }
        };

        match client_msg {
            ClientMessage::Authenticate { token } => {
                let result =
                    handle_authenticate(&state_clone, &token, &tx_clone, &mut player_token, &mut player_id, &client_ip_clone).await;
                if let Err(e) = result {
                    let _ = tx.send(ServerMessage::AuthError { message: e });
                }
            }

            ClientMessage::JoinQueue { game_mode, civilization } => {
                if let Some(ref token) = player_token {
                    handle_join_queue(&state_clone, token, game_mode, civilization, &tx_clone).await;
                } else {
                    let _ = tx.send(ServerMessage::Error {
                        message: "Not authenticated".to_string(),
                    });
                }
            }

            ClientMessage::LeaveQueue => {
                if let Some(pid) = player_id {
                    handle_leave_queue(&state_clone, pid, player_token.as_ref(), &tx_clone).await;
                }
            }

            ClientMessage::Ready => {
                if let Some(pid) = player_id {
                    handle_ready(&state_clone, pid).await;
                }
            }

            ClientMessage::SetHostAddress { address } => {
                if let Some(pid) = player_id {
                    handle_set_host_address(&state_clone, pid, address).await;
                }
            }

            ClientMessage::GameCommand { command } => {
                if let Some(pid) = player_id {
                    state_clone.relay_manager.relay_command(pid, command).await;
                }
            }

            ClientMessage::LeaveMatch => {
                if let Some(pid) = player_id {
                    handle_leave_match(&state_clone, pid, player_token.as_ref()).await;
                }
            }

            ClientMessage::RequestReconnect => {
                if let Some(pid) = player_id {
                    handle_reconnect(&state_clone, pid, &tx_clone).await;
                }
            }

            ClientMessage::MatchEnded { result } => {
                if let Some(pid) = player_id {
                    handle_match_ended(&state_clone, pid, result).await;
                }
            }

            ClientMessage::Ping { timestamp } => {
                let _ = tx.send(ServerMessage::Pong { timestamp });
            }

            ClientMessage::ReportPing { ping_ms } => {
                if let Some(pid) = player_id {
                    state_clone.relay_manager.broadcast_player_ping(pid, ping_ms).await;
                }
            }

            ClientMessage::TabVisibility { visible } => {
                if let Some(pid) = player_id {
                    state_clone.relay_manager.set_player_backgrounded(pid, !visible).await;
                }
            }

            ClientMessage::ChatMessage { channel, text } => {
                if let Some(pid) = player_id {
                    let text: String = text.chars().take(200).collect();
                    let text = text.trim().to_string();
                    if !text.is_empty() {
                        if let Some(session) = state_clone.session_manager.get_player_session(pid).await {
                            let sender_info = session
                                .all_players()
                                .into_iter()
                                .find(|p| p.player_id == pid)
                                .map(|p| (p.game_player_id, p.username.clone()));

                            if let Some((game_player_id, username)) = sender_info {
                                let chat_msg = ServerMessage::ChatMessage {
                                    from_player_id: game_player_id,
                                    from_username: username,
                                    channel: channel.clone(),
                                    text,
                                };

                                if channel == "Team" {
                                    let connections = state_clone.ws_connections.read().await;
                                    'team: for team in &session.teams {
                                        if team.players.iter().any(|p| p.player_id == pid) {
                                            for player in &team.players {
                                                if let Some(tx) = connections.get(&player.player_id) {
                                                    let _ = tx.send(chat_msg.clone());
                                                }
                                            }
                                            break 'team;
                                        }
                                    }
                                } else {
                                    notify_match_players(&state_clone, &session, chat_msg).await;
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    // Handle disconnect
    if let Some(pid) = player_id {
        // Check if player was in an active (InProgress) match — soft disconnect for reconnect
        if let Some(session) = state.session_manager.get_player_session_if_active(pid).await {
            // Record failure metric
            let description = "Player disconnected during active match";
            state.metrics.record_failure(
                FailureType::PlayerDisconnect,
                description.to_string(),
                Some(session.id),
            ).await;

            let timestamp = std::time::SystemTime::now()
                .duration_since(std::time::UNIX_EPOCH)
                .unwrap()
                .as_secs() as i64;
            if let Err(e) = state.metrics_db.insert_failure(
                timestamp,
                "PlayerDisconnect",
                Some(description),
                Some(session.id),
            ).await {
                tracing::error!("Failed to persist failure to DB: {}", e);
            }

            // Soft disconnect — preserve slot for reconnect
            state.session_manager.mark_player_disconnected(pid).await;
            state.relay_manager.soft_disconnect_player(pid).await;

            // Notify remaining players
            notify_match_players(
                &state,
                &session,
                ServerMessage::PlayerDisconnected { player_id: pid },
            ).await;

            // Resolve game_player_id before spawning timeout
            let game_player_id = state.relay_manager.get_game_player_id(pid).await;

            // Spawn 2-minute timeout task
            let state_clone = state.clone();
            let session_id = session.id;
            let player_token_clone = player_token.clone();
            tokio::spawn(async move {
                tokio::time::sleep(Duration::from_secs(120)).await;
                // Check if still disconnected (hasn't reconnected)
                if state_clone.session_manager.is_player_disconnected(pid).await {
                    tracing::info!("Reconnect timeout expired for player {}, ejecting", pid);

                    // Permanent ejection
                    state_clone.session_manager.mark_player_reconnected(pid).await; // clear from disconnected set
                    state_clone.session_manager.remove_player_from_session(pid).await;
                    state_clone.relay_manager.remove_player(pid).await;

                    // Set player state to Idle
                    if let Some(ref token) = player_token_clone {
                        let mut players = state_clone.players.write().await;
                        if let Some(player) = players.get_mut(token) {
                            player.state = PlayerState::Idle;
                        }
                    }

                    // Notify remaining players to surrender this player's units
                    if let Some(gid) = game_player_id {
                        if let Some(session) = state_clone.session_manager.get_session(session_id).await {
                            notify_match_players(
                                &state_clone,
                                &session,
                                ServerMessage::PlayerEjected { player_id: pid, game_player_id: gid },
                            ).await;
                        }
                    }

                    // Check if all players are now gone (no connected, no soft-disconnected)
                    if !state_clone.session_manager.has_any_connected_players(session_id).await {
                        handle_abandoned_match(&state_clone, session_id).await;
                    }
                }
            });

            // Don't set player to Idle — they're still "in match" during reconnect window
        } else if let Some(session) = state.session_manager.get_player_session(pid).await {
            // In a non-InProgress match (WaitingForReady, Starting) — hard disconnect
            if matches!(session.state, MatchState::WaitingForReady | MatchState::Starting) {
                let description = "Player disconnected during match setup";
                state.metrics.record_failure(
                    FailureType::PlayerDisconnect,
                    description.to_string(),
                    Some(session.id),
                ).await;

                let timestamp = std::time::SystemTime::now()
                    .duration_since(std::time::UNIX_EPOCH)
                    .unwrap()
                    .as_secs() as i64;
                let _ = state.metrics_db.insert_failure(
                    timestamp,
                    "PlayerDisconnect",
                    Some(description),
                    Some(session.id),
                ).await;
            }

            // Hard removal
            state.session_manager.remove_player_from_session(pid).await;
            state.relay_manager.remove_player(pid).await;

            notify_match_players(
                &state,
                &session,
                ServerMessage::PlayerDisconnected { player_id: pid },
            ).await;

            if let Some(ref token) = player_token {
                let mut players = state.players.write().await;
                if let Some(player) = players.get_mut(token) {
                    player.state = PlayerState::Idle;
                }
            }
        } else {
            // Not in any match — just clean up queue/relay
            state.relay_manager.remove_player(pid).await;

            if let Some(ref token) = player_token {
                let mut players = state.players.write().await;
                if let Some(player) = players.get_mut(token) {
                    player.state = PlayerState::Idle;
                }
            }
        }

        // Always clean up queue
        let game_mode = state.queue_manager.get_queue_entries().await
            .iter()
            .flat_map(|(_, entries)| entries.iter())
            .find(|e| e.player_id == pid)
            .map(|e| e.game_mode);

        state.queue_manager.remove_from_queue(pid).await;

        // Remove player IP
        state.player_ips.write().await.remove(&pid);

        if let Some(mode) = game_mode {
            broadcast_queue_players(&state, mode).await;
        }
    }

    // Remove WebSocket connection
    if let Some(pid) = player_id {
        state.ws_connections.write().await.remove(&pid);
    }

    send_task.abort();
}

async fn handle_authenticate(
    state: &Arc<AppState>,
    token: &str,
    tx: &mpsc::UnboundedSender<ServerMessage>,
    player_token: &mut Option<String>,
    player_id: &mut Option<Uuid>,
    client_ip: &str,
) -> Result<(), String> {
    let player = {
        let players = state.players.read().await;
        players.get(token).cloned()
    };

    match player {
        Some(p) => {
            *player_token = Some(token.to_string());
            *player_id = Some(p.id);

            // Store player IP for geolocation
            state.player_ips.write().await.insert(p.id, client_ip.to_string());

            state.ws_connections.write().await.insert(p.id, tx.clone());

            let _ = tx.send(ServerMessage::Authenticated {
                player_id: p.id,
                username: p.username,
            });
            Ok(())
        }
        None => Err("Invalid token".to_string()),
    }
}

async fn handle_join_queue(
    state: &Arc<AppState>,
    token: &str,
    game_mode: GameMode,
    civilization: u8,
    tx: &mpsc::UnboundedSender<ServerMessage>,
) {
    let player = {
        let players = state.players.read().await;
        players.get(token).cloned()
    };

    if let Some(player) = player {
        let entry = QueueEntry::new(player.id, player.username.clone(), game_mode, civilization);
        let position = state.queue_manager.add_to_queue(entry).await;

        {
            let mut players = state.players.write().await;
            if let Some(p) = players.get_mut(token) {
                p.state = PlayerState::InQueue;
            }
        }

        let _ = tx.send(ServerMessage::QueueJoined { game_mode, position });
        broadcast_queue_players(state, game_mode).await;
    }
}

async fn handle_leave_queue(
    state: &Arc<AppState>,
    player_id: Uuid,
    token: Option<&String>,
    tx: &mpsc::UnboundedSender<ServerMessage>,
) {
    // Capture the game mode before removing so we can broadcast to remaining players
    let game_mode = state.queue_manager.get_queue_entries().await
        .iter()
        .flat_map(|(_, entries)| entries.iter())
        .find(|e| e.player_id == player_id)
        .map(|e| e.game_mode);

    state.queue_manager.remove_from_queue(player_id).await;

    if let Some(token) = token {
        let mut players = state.players.write().await;
        if let Some(p) = players.get_mut(token) {
            p.state = PlayerState::Idle;
        }
    }

    let _ = tx.send(ServerMessage::QueueLeft);

    if let Some(mode) = game_mode {
        broadcast_queue_players(state, mode).await;
    }
}

async fn handle_ready(state: &Arc<AppState>, player_id: Uuid) {
    if let Some((session, all_ready)) = state.session_manager.set_player_ready(player_id).await {
        notify_match_players(state, &session, ServerMessage::PlayerReady { player_id }).await;

        if all_ready {
            // Record match start time for duration tracking
            state.metrics.record_match_started(session.id).await;

            notify_match_players(
                state,
                &session,
                ServerMessage::MatchStarting {
                    match_id: session.id,
                },
            )
            .await;
        }
    }
}

async fn handle_set_host_address(state: &Arc<AppState>, player_id: Uuid, address: String) {
    if let Some(session) = state.session_manager.set_host_address(player_id, address.clone()).await
    {
        notify_match_players(
            state,
            &session,
            ServerMessage::HostAddressSet { address },
        )
        .await;
    }
}

async fn handle_leave_match(state: &Arc<AppState>, player_id: Uuid, token: Option<&String>) {
    if let Some(session) = state.session_manager.remove_player_from_session(player_id).await {
        state.relay_manager.remove_player(player_id).await;

        notify_match_players(
            state,
            &session,
            ServerMessage::PlayerDisconnected { player_id },
        )
        .await;
    }

    if let Some(token) = token {
        let mut players = state.players.write().await;
        if let Some(p) = players.get_mut(token) {
            p.state = PlayerState::Idle;
        }
    }
}

async fn handle_match_ended(state: &Arc<AppState>, player_id: Uuid, result: ClientMatchResult) {
    // Get the player's session
    if let Some(session) = state.session_manager.get_player_session(player_id).await {
        // Get participant countries, cities, and usernames
        let player_ips = state.player_ips.read().await;
        let participants: Vec<(String, String, String)> = session
            .all_players()
            .iter()
            .map(|p| {
                let (country, city) = player_ips.get(&p.player_id)
                    .and_then(|ip| state.geolocation.lookup_country(ip))
                    .unwrap_or_else(|| (String::new(), None));
                (p.username.clone(), country, city.unwrap_or_default())
            })
            .collect();
        drop(player_ips);

        let participant_usernames: Vec<String> = participants.iter().map(|(u, _, _)| u.clone()).collect();
        let participant_countries: Vec<String> = participants.iter().map(|(_, c, _)| c.clone()).collect();
        let participant_cities: Vec<String> = participants.iter().map(|(_, _, city)| city.clone()).collect();

        // Convert client result to metrics result
        let metrics_result = match result {
            ClientMatchResult::Victory { winning_team } => MetricsMatchResult::Victory { winning_team },
            ClientMatchResult::Surrender => MetricsMatchResult::Surrender,
            ClientMatchResult::Draw => MetricsMatchResult::Draw,
        };

        // Record the match completion (returns duration_ms)
        let duration_ms = state.metrics.record_match_completed_with_result(
            session.game_mode,
            session.id,
            metrics_result.clone(),
            participant_countries.clone(),
            participant_usernames.clone(),
            participant_cities.clone(),
        ).await;

        // Persist to database
        let timestamp = std::time::SystemTime::now()
            .duration_since(std::time::UNIX_EPOCH)
            .unwrap()
            .as_secs() as i64;
        let player_count = session.all_players().len() as i32;
        if let Err(e) = state.metrics_db.insert_match_event(
            session.id,
            MatchEventType::Completed,
            session.game_mode,
            Some(player_count),
            duration_ms.map(|d| d as i64),
            Some(&metrics_result),
            &participant_countries,
            &participant_usernames,
            &participant_cities,
            timestamp,
        ).await {
            tracing::error!("Failed to persist match completion to DB: {}", e);
        }

        // Broadcast to dashboard clients
        let participant_locations: Vec<ParticipantLocation> = participants
            .iter()
            .map(|(username, country, city)| ParticipantLocation {
                country_code: country.clone(),
                username: username.clone(),
                city: if city.is_empty() { None } else { Some(city.clone()) },
            })
            .collect();
        broadcast_dashboard_update(
            state,
            DashboardMessage::MatchEvent(RecentMatchEvent {
                match_id: Some(session.id.to_string()),
                timestamp: 0,
                wall_clock_time: timestamp as u64,
                game_mode: game_mode_to_string(&session.game_mode),
                event_type: "completed".to_string(),
                duration_ms,
                result: Some(match_result_to_string(&metrics_result)),
                participant_locations,
            }),
        )
        .await;

        // Clean up the session
        state.session_manager.complete_session(session.id).await;
    }
}

async fn handle_reconnect(
    state: &Arc<AppState>,
    player_id: Uuid,
    tx: &mpsc::UnboundedSender<ServerMessage>,
) {
    // Check if player has an active session and is marked as disconnected
    if let Some(session) = state.session_manager.get_player_session_if_active(player_id).await {
        if state.session_manager.is_player_disconnected(player_id).await {
            // Re-attach to relay with the existing tx as the sender
            let command_history = state.relay_manager.reconnect_player(player_id, tx.clone()).await;
            state.session_manager.mark_player_reconnected(player_id).await;

            // Resolve game_player_id
            let game_player_id = state.relay_manager.get_game_player_id(player_id).await.unwrap_or(0);

            // Convert command history to ReconnectFrame format
            let reconnect_frames: Vec<ReconnectFrame> = command_history
                .into_iter()
                .map(|(frame, commands)| ReconnectFrame {
                    frame,
                    commands: commands
                        .into_iter()
                        .map(|(from_player_id, command)| ReconnectCommand {
                            from_player_id,
                            command,
                        })
                        .collect(),
                })
                .collect();

            // Send reconnect data
            let _ = tx.send(ServerMessage::ReconnectAvailable {
                match_id: session.id,
                game_mode: session.game_mode,
                teams: session.teams.clone(),
                connection_info: session.connection_info.clone(),
                your_game_player_id: game_player_id,
                command_history: reconnect_frames,
            });

            // Notify other players
            notify_match_players(
                state,
                &session,
                ServerMessage::PlayerReconnected { player_id },
            ).await;

            tracing::info!("Player {} reconnected to match {}", player_id, session.id);
            return;
        }
    }

    // No active session or not disconnected
    let _ = tx.send(ServerMessage::ReconnectUnavailable);
}

async fn handle_abandoned_match(state: &Arc<AppState>, session_id: Uuid) {
    if let Some(session) = state.session_manager.get_session(session_id).await {
        tracing::info!(
            "All players gone from match {}, cleaning up abandoned match",
            session.id
        );

        let timestamp = std::time::SystemTime::now()
            .duration_since(std::time::UNIX_EPOCH)
            .unwrap()
            .as_secs() as i64;

        let player_ips = state.player_ips.read().await;
        let participants: Vec<(String, String, String)> = session
            .all_players()
            .iter()
            .map(|p| {
                let (country, city) = player_ips.get(&p.player_id)
                    .and_then(|ip| state.geolocation.lookup_country(ip))
                    .unwrap_or_else(|| (String::new(), None));
                (p.username.clone(), country, city.unwrap_or_default())
            })
            .collect();
        drop(player_ips);

        let participant_usernames: Vec<String> = participants.iter().map(|(u, _, _)| u.clone()).collect();
        let participant_countries: Vec<String> = participants.iter().map(|(_, c, _)| c.clone()).collect();
        let participant_cities: Vec<String> = participants.iter().map(|(_, _, city)| city.clone()).collect();

        let duration_ms = state.metrics.record_match_abandoned(
            session.game_mode,
            session.id,
            participant_countries.clone(),
            participant_usernames.clone(),
            participant_cities.clone(),
        ).await;

        let player_count = session.all_players().len() as i32;
        let _ = state.metrics_db.insert_match_event(
            session.id,
            MatchEventType::Completed,
            session.game_mode,
            Some(player_count),
            duration_ms.map(|d| d as i64),
            Some(&MetricsMatchResult::Abandoned),
            &participant_countries,
            &participant_usernames,
            &participant_cities,
            timestamp,
        ).await;

        let _ = state.metrics_db.insert_failure(
            timestamp,
            "MatchAbandoned",
            Some("Match abandoned - all players disconnected"),
            Some(session.id),
        ).await;

        let participant_locations: Vec<ParticipantLocation> = participants
            .iter()
            .map(|(username, country, city)| ParticipantLocation {
                country_code: country.clone(),
                username: username.clone(),
                city: if city.is_empty() { None } else { Some(city.clone()) },
            })
            .collect();
        broadcast_dashboard_update(
            state,
            DashboardMessage::MatchEvent(RecentMatchEvent {
                match_id: Some(session.id.to_string()),
                timestamp: 0,
                wall_clock_time: timestamp as u64,
                game_mode: game_mode_to_string(&session.game_mode),
                event_type: "completed".to_string(),
                duration_ms,
                result: Some("Abandoned".to_string()),
                participant_locations,
            }),
        ).await;

        state.session_manager.complete_session(session.id).await;
    }
}

async fn broadcast_queue_players(state: &Arc<AppState>, game_mode: GameMode) {
    let entries = state.queue_manager.get_queue_entries().await;
    let queue = match entries.get(&game_mode) {
        Some(q) => q,
        None => return,
    };

    let players: Vec<String> = queue.iter().map(|e| e.username.clone()).collect();
    let player_ids: Vec<Uuid> = queue.iter().map(|e| e.player_id).collect();
    let msg = ServerMessage::QueuePlayersUpdate { players };

    let connections = state.ws_connections.read().await;
    for pid in &player_ids {
        if let Some(tx) = connections.get(pid) {
            let _ = tx.send(msg.clone());
        }
    }
}

async fn notify_match_players(state: &Arc<AppState>, session: &MatchSession, message: ServerMessage) {
    let connections = state.ws_connections.read().await;
    for player in session.all_players() {
        if let Some(tx) = connections.get(&player.player_id) {
            let _ = tx.send(message.clone());
        }
    }
}

pub async fn handle_match_found(state: Arc<AppState>, session: MatchSession) {
    let connections = state.ws_connections.read().await;

    // Get participant countries, cities, and usernames for this match
    let player_ips = state.player_ips.read().await;
    let participants: Vec<(String, String, String)> = session
        .all_players()
        .iter()
        .map(|p| {
            let (country, city) = player_ips.get(&p.player_id)
                .and_then(|ip| state.geolocation.lookup_country(ip))
                .unwrap_or_else(|| (String::new(), None));
            (p.username.clone(), country, city.unwrap_or_default())
        })
        .collect();
    drop(player_ips);

    let participant_usernames: Vec<String> = participants.iter().map(|(u, _, _)| u.clone()).collect();
    let participant_countries: Vec<String> = participants.iter().map(|(_, c, _)| c.clone()).collect();
    let participant_cities: Vec<String> = participants.iter().map(|(_, _, city)| city.clone()).collect();

    // Record match created event with participant countries, usernames, and cities
    state.metrics.record_match_event(
        session.game_mode,
        MatchEventType::Created,
        Some(session.id),
        participant_countries.clone(),
        participant_usernames.clone(),
        participant_cities.clone(),
    ).await;

    // Persist to database
    let timestamp = std::time::SystemTime::now()
        .duration_since(std::time::UNIX_EPOCH)
        .unwrap()
        .as_secs() as i64;
    let player_count = session.all_players().len() as i32;
    if let Err(e) = state.metrics_db.insert_match_event(
        session.id,
        MatchEventType::Created,
        session.game_mode,
        Some(player_count),
        None,
        None,
        &participant_countries,
        &participant_usernames,
        &participant_cities,
        timestamp,
    ).await {
        tracing::error!("Failed to persist match creation to DB: {}", e);
    }

    // Broadcast to dashboard clients
    let participant_locations: Vec<ParticipantLocation> = participants
        .iter()
        .map(|(username, country, city)| ParticipantLocation {
            country_code: country.clone(),
            username: username.clone(),
            city: if city.is_empty() { None } else { Some(city.clone()) },
        })
        .collect();
    broadcast_dashboard_update(
        &state,
        DashboardMessage::MatchEvent(RecentMatchEvent {
            match_id: Some(session.id.to_string()),
            timestamp: 0,
            wall_clock_time: timestamp as u64,
            game_mode: game_mode_to_string(&session.game_mode),
            event_type: "created".to_string(),
            duration_ms: None,
            result: None,
            participant_locations,
        }),
    )
    .await;

    if session.game_mode.uses_relay() {
        if let ConnectionInfo::Relay { relay_session_id } = &session.connection_info {
            let player_count = session.all_players().len();
            state.relay_manager.create_session(*relay_session_id, player_count).await;
        }
    }

    for player in session.all_players() {
        if let Some(tx) = connections.get(&player.player_id) {
            let msg = ServerMessage::MatchFound {
                match_id: session.id,
                game_mode: session.game_mode,
                teams: session.teams.clone(),
                connection_info: session.connection_info.clone(),
                your_game_player_id: player.game_player_id,
            };
            let _ = tx.send(msg);

            if session.game_mode.uses_relay() {
                if let ConnectionInfo::Relay { relay_session_id } = &session.connection_info {
                    state
                        .relay_manager
                        .add_player_to_session(
                            *relay_session_id,
                            player.player_id,
                            player.game_player_id,
                            tx.clone(),
                        )
                        .await;
                }
            }
        }
    }
}

pub async fn handle_player_joined(
    state: Arc<AppState>,
    session: MatchSession,
    new_player: TeamPlayer,
    team_id: u8,
) {
    let connections = state.ws_connections.read().await;

    // Send MatchFound to the new player (with current teams and their game_player_id)
    if let Some(tx) = connections.get(&new_player.player_id) {
        let msg = ServerMessage::MatchFound {
            match_id: session.id,
            game_mode: session.game_mode,
            teams: session.teams.clone(),
            connection_info: session.connection_info.clone(),
            your_game_player_id: new_player.game_player_id,
        };
        let _ = tx.send(msg);

        // Register with relay
        if session.game_mode.uses_relay() {
            if let ConnectionInfo::Relay { relay_session_id } = &session.connection_info {
                state
                    .relay_manager
                    .add_player_to_session(
                        *relay_session_id,
                        new_player.player_id,
                        new_player.game_player_id,
                        tx.clone(),
                    )
                    .await;

                // Update relay player count
                let player_count = session.all_players().len();
                state
                    .relay_manager
                    .update_session_player_count(*relay_session_id, player_count)
                    .await;
            }
        }
    }

    // Notify all existing players in the session about the new player
    let notify_msg = ServerMessage::PlayerJoinedMatch {
        player_id: new_player.player_id,
        username: new_player.username.clone(),
        game_player_id: new_player.game_player_id,
        team_id,
        civilization: new_player.civilization,
    };

    for player in session.all_players() {
        if player.player_id != new_player.player_id {
            if let Some(tx) = connections.get(&player.player_id) {
                let _ = tx.send(notify_msg.clone());
            }
        }
    }
}
