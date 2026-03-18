// Dashboard WebSocket client for multiple servers

// Country code to name mapping for flag tooltips
const COUNTRY_NAMES = {
    'US': 'United States', 'GB': 'United Kingdom', 'DE': 'Germany',
    'FR': 'France', 'JP': 'Japan', 'CN': 'China', 'IN': 'India',
    'BR': 'Brazil', 'CA': 'Canada', 'AU': 'Australia', 'SG': 'Singapore',
    'KR': 'South Korea', 'NL': 'Netherlands', 'SE': 'Sweden', 'NO': 'Norway',
    'DK': 'Denmark', 'FI': 'Finland', 'PL': 'Poland', 'ES': 'Spain',
    'IT': 'Italy', 'RU': 'Russia', 'MX': 'Mexico', 'AR': 'Argentina',
    'CL': 'Chile', 'CO': 'Colombia', 'PE': 'Peru', 'VE': 'Venezuela',
    'ZA': 'South Africa', 'EG': 'Egypt', 'NG': 'Nigeria', 'KE': 'Kenya',
    'TH': 'Thailand', 'VN': 'Vietnam', 'PH': 'Philippines', 'MY': 'Malaysia',
    'ID': 'Indonesia', 'TW': 'Taiwan', 'HK': 'Hong Kong', 'NZ': 'New Zealand',
    'IE': 'Ireland', 'PT': 'Portugal', 'BE': 'Belgium', 'CH': 'Switzerland',
    'AT': 'Austria', 'CZ': 'Czech Republic', 'GR': 'Greece', 'TR': 'Turkey',
    'IL': 'Israel', 'AE': 'United Arab Emirates', 'SA': 'Saudi Arabia',
    'PK': 'Pakistan', 'BD': 'Bangladesh', 'UA': 'Ukraine', 'RO': 'Romania',
    'HU': 'Hungary', 'SK': 'Slovakia', 'HR': 'Croatia', 'RS': 'Serbia'
};

// Server configuration
const SERVERS = [
    { id: 'virginia', name: 'Virginia', url: 'openempires-virginia.onrender.com', color: '#22c55e' },
    { id: 'singapore', name: 'Singapore', url: 'openempires-49g9.onrender.com', color: '#3b82f6' },
    { id: 'oregon', name: 'Oregon', url: 'openempires.onrender.com', color: '#f59e0b' },
    { id: 'frankfurt', name: 'Frankfurt', url: 'openempires-6f10.onrender.com', color: '#ec4899' }
];

class MultiServerDashboard {
    constructor() {
        this.connections = {};  // { serverId: WebSocket }
        this.serverStates = {}; // { serverId: state data }
        this.connectionStatus = {}; // { serverId: boolean }
        this.reconnectAttempts = {}; // { serverId: number }
        this.maxReconnectAttempts = 10;
        this.reconnectDelay = 1000;
        this.queueSizeChart = null;
        this.matchesPerMinuteChart = null;
        this.weeklyMatchChart = null;
        this.aggregatedQueueHistory = [];
        this.maxQueueHistoryPoints = 1800; // 30 minutes at 1-second intervals
        this.allEvents = []; // Combined events from all servers
        this.allFailures = []; // Combined failures from all servers
        this.allActiveMatches = []; // Combined active matches from all servers
        this.maxEvents = 50;
        this.maxFailures = 20;
        this.historicalDataLoaded = false;  // Track if we've loaded historical data from first server

        // Initialize state for each server
        for (const server of SERVERS) {
            this.serverStates[server.id] = null;
            this.connectionStatus[server.id] = false;
            this.reconnectAttempts[server.id] = 0;
        }

        this.init();
    }

    init() {
        this.initCharts();
        this.connectAll();
        // Update aggregated queue history every second
        setInterval(() => this.updateAggregatedQueueHistory(), 1000);
        // Update active match timers every second
        setInterval(() => this.renderActiveMatches(), 1000);
    }

    getWebSocketUrl(server) {
        // Check if we're running locally
        const isLocal = window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1';
        const currentHost = window.location.host;

        // If local and this is the "main" server config, connect to local server
        // Otherwise connect to remote servers via WSS
        if (isLocal) {
            // For local development, check if we should connect locally
            // We'll connect to localhost for the first matching server based on current URL
            const localUrl = `ws://${currentHost}/ws/dashboard`;
            const remoteUrl = `wss://${server.url}/ws/dashboard`;

            // Connect to local server if the page is served from localhost
            // and also try to connect to remote servers
            if (server.id === 'oregon' && !server.url.includes('virginia') && !server.url.includes('49g9') && !server.url.includes('6f10')) {
                // For "oregon" server when running locally, connect to localhost
                return localUrl;
            }
            return remoteUrl;
        }

        // When running on a deployed server, check if current host matches this server
        if (currentHost === server.url || window.location.hostname === server.url.split(':')[0]) {
            const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
            return `${protocol}//${currentHost}/ws/dashboard`;
        }

        // Otherwise connect via WSS to remote server
        return `wss://${server.url}/ws/dashboard`;
    }

    connectAll() {
        for (const server of SERVERS) {
            this.connect(server);
        }
    }

    connect(server) {
        const url = this.getWebSocketUrl(server);
        console.log(`Connecting to ${server.name} (${server.id}):`, url);

        try {
            const ws = new WebSocket(url);
            this.connections[server.id] = ws;

            ws.onopen = () => {
                console.log(`${server.name} WebSocket connected`);
                this.reconnectAttempts[server.id] = 0;
                this.connectionStatus[server.id] = true;
                this.updateServerConnectionStatus(server.id, true);
                this.updateGlobalConnectionStatus();
            };

            ws.onmessage = (event) => {
                try {
                    const message = JSON.parse(event.data);
                    this.handleMessage(server.id, message);
                } catch (e) {
                    console.error(`Failed to parse message from ${server.name}:`, e);
                }
            };

            ws.onclose = () => {
                console.log(`${server.name} WebSocket disconnected`);
                this.connectionStatus[server.id] = false;
                this.updateServerConnectionStatus(server.id, false);
                this.updateGlobalConnectionStatus();
                this.scheduleReconnect(server);
            };

            ws.onerror = (error) => {
                console.error(`${server.name} WebSocket error:`, error);
            };
        } catch (e) {
            console.error(`Failed to connect to ${server.name}:`, e);
            this.scheduleReconnect(server);
        }
    }

    scheduleReconnect(server) {
        if (this.reconnectAttempts[server.id] >= this.maxReconnectAttempts) {
            console.log(`Max reconnect attempts reached for ${server.name}`);
            return;
        }

        this.reconnectAttempts[server.id]++;
        const delay = this.reconnectDelay * Math.pow(2, this.reconnectAttempts[server.id] - 1);
        console.log(`Reconnecting to ${server.name} in ${delay}ms (attempt ${this.reconnectAttempts[server.id]})`);

        setTimeout(() => this.connect(server), delay);
    }

    updateServerConnectionStatus(serverId, connected) {
        const card = document.querySelector(`.server-card[data-server="${serverId}"]`);
        if (!card) return;

        if (connected) {
            card.classList.remove('disconnected');
            card.classList.add('connected');
        } else {
            card.classList.remove('connected');
            card.classList.add('disconnected');
            // Reset stats display when disconnected
            this.updateServerCard(serverId, null);
        }
    }

    updateGlobalConnectionStatus() {
        const connectedCount = Object.values(this.connectionStatus).filter(Boolean).length;
        const totalCount = SERVERS.length;

        const statusEl = document.getElementById('connectionStatus');
        const indicator = statusEl.querySelector('.status-indicator');
        const text = statusEl.querySelector('.status-text');

        document.getElementById('serversConnected').textContent = `${connectedCount}/${totalCount}`;

        if (connectedCount === totalCount) {
            indicator.classList.remove('disconnected', 'partial');
            indicator.classList.add('connected');
            text.textContent = 'All Connected';
        } else if (connectedCount > 0) {
            indicator.classList.remove('disconnected', 'connected');
            indicator.classList.add('partial');
            text.textContent = `${connectedCount}/${totalCount} Connected`;
        } else {
            indicator.classList.remove('connected', 'partial');
            indicator.classList.add('disconnected');
            text.textContent = 'Disconnected';
        }
    }

    handleMessage(serverId, message) {
        switch (message.type) {
            case 'FullState':
                this.handleFullState(serverId, message.data);
                break;
            case 'QueueUpdate':
                this.handleQueueUpdate(serverId, message.data);
                break;
            case 'LoadUpdate':
                this.handleLoadUpdate(serverId, message.data);
                break;
            case 'MatchEvent':
                this.handleMatchEvent(serverId, message.data);
                break;
            case 'FailureEvent':
                this.handleFailureEvent(serverId, message.data);
                break;
            default:
                console.log(`Unknown message type from ${serverId}:`, message.type);
        }
    }

    handleFullState(serverId, state) {
        this.serverStates[serverId] = state;
        this.updateServerCard(serverId, state);
        this.updateGlobalSummary();
        this.updateMatchesPerMinuteChart();
        this.updateWeeklyMatchChart();
        this.updateServerHealth();

        // Only load historical events from the FIRST server that connects
        // (all servers share the same DB, so we'd get duplicates otherwise)
        if (!this.historicalDataLoaded) {
            this.historicalDataLoaded = true;

            if (state.recent_events) {
                for (const event of state.recent_events) {
                    this.addEvent(serverId, event, false);
                }
                this.renderEvents();
            }

            if (state.server_health && state.server_health.recent_failures) {
                for (const failure of state.server_health.recent_failures) {
                    this.addFailure(serverId, failure, false);
                }
                this.renderFailures();
            }
        }

        // Always update active matches (they're per-server, not from shared DB)
        this.updateActiveMatches();
    }

    handleQueueUpdate(serverId, data) {
        if (!this.serverStates[serverId]) {
            this.serverStates[serverId] = {};
        }
        this.serverStates[serverId].queues = data.queues;
        this.updateServerCard(serverId, this.serverStates[serverId]);
        this.updateGlobalSummary();
    }

    handleLoadUpdate(serverId, load) {
        if (!this.serverStates[serverId]) {
            this.serverStates[serverId] = {};
        }
        this.serverStates[serverId].server_load = load;
        this.updateServerCard(serverId, this.serverStates[serverId]);
        this.updateGlobalSummary();
    }

    handleMatchEvent(serverId, event) {
        this.addEvent(serverId, event, true);
        this.renderEvents();

        // When a match is completed, remove it from active matches
        if (event.event_type === 'completed' && event.match_id) {
            this.allActiveMatches = this.allActiveMatches.filter(m => m.match_id !== event.match_id);
            this.renderActiveMatches();
        }

        // Update global summary (match counts may have changed)
        this.updateGlobalSummary();
    }

    handleFailureEvent(serverId, failure) {
        this.addFailure(serverId, failure, true);
        this.renderFailures();
        this.updateServerHealth();
    }

    updateServerCard(serverId, state) {
        const onlineEl = document.getElementById(`${serverId}-online`);
        const queueEl = document.getElementById(`${serverId}-queue`);
        const matchEl = document.getElementById(`${serverId}-match`);
        const queuesEl = document.getElementById(`${serverId}-queues`);

        if (!state || !this.connectionStatus[serverId]) {
            if (onlineEl) onlineEl.textContent = '-';
            if (queueEl) queueEl.textContent = '-';
            if (matchEl) matchEl.textContent = '-';
            if (queuesEl) {
                queuesEl.innerHTML = `
                    <span class="queue-mode">1v1: -</span>
                    <span class="queue-mode">2v2: -</span>
                    <span class="queue-mode">3v3: -</span>
                    <span class="queue-mode">4v4: -</span>
                `;
            }
            return;
        }

        // Update load stats
        if (state.server_load) {
            if (onlineEl) onlineEl.textContent = state.server_load.active_connections;
            if (queueEl) queueEl.textContent = state.server_load.players_in_queue;
            if (matchEl) matchEl.textContent = state.server_load.players_in_match;
        }

        // Update queue breakdown
        if (state.queues && queuesEl) {
            const queueCounts = { '1v1': 0, '2v2': 0, '3v3': 0, '4v4': 0 };
            for (const queue of state.queues) {
                queueCounts[queue.game_mode] = queue.player_count;
            }
            queuesEl.innerHTML = `
                <span class="queue-mode q-1v1">1v1: ${queueCounts['1v1']}</span>
                <span class="queue-mode q-2v2">2v2: ${queueCounts['2v2']}</span>
                <span class="queue-mode q-3v3">3v3: ${queueCounts['3v3']}</span>
                <span class="queue-mode q-4v4">4v4: ${queueCounts['4v4']}</span>
            `;
        }
    }

    updateGlobalSummary() {
        let totalOnline = 0;
        let totalInQueue = 0;
        let totalInMatch = 0;
        let totalMatches = 0;
        let totalDuration = 0;
        let durationCount = 0;

        for (const serverId of Object.keys(this.serverStates)) {
            const state = this.serverStates[serverId];
            if (state && this.connectionStatus[serverId]) {
                if (state.server_load) {
                    totalOnline += state.server_load.active_connections || 0;
                    totalInQueue += state.server_load.players_in_queue || 0;
                    totalInMatch += state.server_load.players_in_match || 0;
                }
                if (state.match_stats) {
                    totalMatches += state.match_stats.total_created || 0;
                    if (state.match_stats.average_duration_ms) {
                        totalDuration += state.match_stats.average_duration_ms;
                        durationCount++;
                    }
                }
            }
        }

        document.getElementById('totalOnline').textContent = totalOnline;
        document.getElementById('totalInQueue').textContent = totalInQueue;
        document.getElementById('totalInMatch').textContent = totalInMatch;
        document.getElementById('totalMatchesCreated').textContent = totalMatches;

        // Calculate average duration across servers
        if (durationCount > 0) {
            const avgDuration = totalDuration / durationCount;
            document.getElementById('avgMatchDuration').textContent = this.formatDuration(avgDuration);
        } else {
            document.getElementById('avgMatchDuration').textContent = '-';
        }
    }

    updateServerHealth() {
        let matchTimeouts = 0;
        let playerDisconnects = 0;
        let relayErrors = 0;

        for (const serverId of Object.keys(this.serverStates)) {
            const state = this.serverStates[serverId];
            if (state && state.server_health && state.server_health.failure_counts && this.connectionStatus[serverId]) {
                matchTimeouts += state.server_health.failure_counts['MatchTimeout'] || 0;
                playerDisconnects += state.server_health.failure_counts['PlayerDisconnect'] || 0;
                relayErrors += state.server_health.failure_counts['RelayError'] || 0;
            }
        }

        document.getElementById('matchTimeoutCount').textContent = matchTimeouts;
        document.getElementById('playerDisconnectCount').textContent = playerDisconnects;
        document.getElementById('relayErrorCount').textContent = relayErrors;
    }

    updateAggregatedQueueHistory() {
        // Aggregate current queue sizes from all connected servers
        const aggregated = { '1v1': 0, '2v2': 0, '3v3': 0, '4v4': 0 };

        for (const serverId of Object.keys(this.serverStates)) {
            const state = this.serverStates[serverId];
            if (state && state.queues && this.connectionStatus[serverId]) {
                for (const queue of state.queues) {
                    aggregated[queue.game_mode] = (aggregated[queue.game_mode] || 0) + queue.player_count;
                }
            }
        }

        this.aggregatedQueueHistory.push({ timestamp: Date.now(), sizes: aggregated });
        if (this.aggregatedQueueHistory.length > this.maxQueueHistoryPoints) {
            this.aggregatedQueueHistory.shift();
        }

        this.updateQueueSizeChart();
    }

    addEvent(serverId, event, isNew) {
        const server = SERVERS.find(s => s.id === serverId);
        const eventWithServer = {
            ...event,
            serverId,
            serverName: server ? server.name : serverId,
            serverColor: server ? server.color : '#888',
            receivedAt: Date.now()
        };

        if (isNew) {
            this.allEvents.unshift(eventWithServer);
        } else {
            this.allEvents.push(eventWithServer);
        }

        // Sort by wall_clock_time (newest first) and limit
        this.allEvents.sort((a, b) => (b.wall_clock_time || b.receivedAt) - (a.wall_clock_time || a.receivedAt));
        if (this.allEvents.length > this.maxEvents) {
            this.allEvents = this.allEvents.slice(0, this.maxEvents);
        }
    }

    addFailure(serverId, failure, isNew) {
        const server = SERVERS.find(s => s.id === serverId);
        const failureWithServer = {
            ...failure,
            serverId,
            serverName: server ? server.name : serverId,
            serverColor: server ? server.color : '#888',
            receivedAt: Date.now()
        };

        if (isNew) {
            this.allFailures.unshift(failureWithServer);
        } else {
            this.allFailures.push(failureWithServer);
        }

        // Sort by wall_clock_time (newest first) and limit
        this.allFailures.sort((a, b) => (b.wall_clock_time || b.receivedAt) - (a.wall_clock_time || a.receivedAt));
        if (this.allFailures.length > this.maxFailures) {
            this.allFailures = this.allFailures.slice(0, this.maxFailures);
        }
    }

    renderEvents() {
        const container = document.getElementById('eventsList');
        container.innerHTML = '';

        if (this.allEvents.length === 0) {
            container.innerHTML = '<div class="event-item placeholder">Waiting for events...</div>';
            return;
        }

        for (const event of this.allEvents.slice(0, 20)) {
            const div = document.createElement('div');
            const isSinglePlayer = event.game_mode === 'single_player';
            div.className = `event-item event-${event.event_type}${isSinglePlayer ? ' event-single-player' : ''}`;

            const typeLabel = this.formatEventType(event.event_type);
            const timeStr = event.wall_clock_time ? this.formatTime(event.wall_clock_time * 1000) : '';
            const durationStr = event.duration_ms ? this.formatDuration(event.duration_ms) : '';
            const flagsStr = this.renderFlags(event.participant_locations || [], event.game_mode);
            const modeLabel = this.formatGameMode(event.game_mode);
            const modeClass = isSinglePlayer ? 'event-mode mode-single-player' : 'event-mode';
            const resultStr = event.result ? `<span class="event-result">${this.formatResult(event.result, isSinglePlayer)}</span>` : '';

            div.innerHTML = `
                <span class="event-time">${timeStr}</span>
                <span class="event-server" style="background-color: ${event.serverColor}20; color: ${event.serverColor}; border-color: ${event.serverColor}">${event.serverName}</span>
                <span class="event-type">${typeLabel}</span>
                <span class="${modeClass}">${modeLabel}</span>
                ${flagsStr ? `<span class="event-flags">${flagsStr}</span>` : ''}
                ${durationStr ? `<span class="event-duration">${durationStr}</span>` : ''}
                ${resultStr}
            `;

            container.appendChild(div);
        }
    }

    renderFailures() {
        const container = document.getElementById('failuresList');
        container.innerHTML = '';

        if (this.allFailures.length === 0) {
            container.innerHTML = '<div class="failure-item placeholder">No recent failures</div>';
            return;
        }

        for (const failure of this.allFailures.slice(0, 10)) {
            const div = document.createElement('div');
            div.className = `failure-item failure-${failure.failure_type.toLowerCase()}`;

            const timeStr = failure.wall_clock_time ? this.formatTime(failure.wall_clock_time * 1000) : '';

            div.innerHTML = `
                <span class="failure-time">${timeStr}</span>
                <span class="failure-server" style="background-color: ${failure.serverColor}20; color: ${failure.serverColor}; border-color: ${failure.serverColor}">${failure.serverName}</span>
                <span class="failure-type">${failure.failure_type}</span>
                <span class="failure-desc">${failure.description}</span>
            `;

            container.appendChild(div);
        }
    }

    updateActiveMatches() {
        // Aggregate active matches from all connected servers
        this.allActiveMatches = [];

        for (const serverId of Object.keys(this.serverStates)) {
            const state = this.serverStates[serverId];
            if (state && state.active_matches && this.connectionStatus[serverId]) {
                const server = SERVERS.find(s => s.id === serverId);
                for (const match of state.active_matches) {
                    this.allActiveMatches.push({
                        ...match,
                        serverId,
                        serverName: server ? server.name : serverId,
                        serverColor: server ? server.color : '#888'
                    });
                }
            }
        }

        // Sort by start_time (oldest first to show longest running matches at top)
        this.allActiveMatches.sort((a, b) => {
            const timeA = a.start_time || a.created_time;
            const timeB = b.start_time || b.created_time;
            return timeA - timeB;
        });

        this.renderActiveMatches();
    }

    renderActiveMatches() {
        const container = document.getElementById('activeMatchesList');
        if (!container) return;

        container.innerHTML = '';

        if (this.allActiveMatches.length === 0) {
            container.innerHTML = '<div class="match-item placeholder">No active matches</div>';
            return;
        }

        const now = Math.floor(Date.now() / 1000); // Current time in seconds

        for (const match of this.allActiveMatches) {
            const div = document.createElement('div');
            div.className = 'match-item';

            const flagsStr = this.renderFlags(match.participant_locations || [], match.game_mode);

            // Calculate elapsed time
            // If match has start_time, it's in progress (all players ready)
            // If only created_time, it's still waiting for players
            const isInProgress = match.start_time > 0;
            const startTime = isInProgress ? match.start_time : match.created_time;
            const elapsedSeconds = now - startTime;
            const timerStr = this.formatElapsedTime(elapsedSeconds);

            const statusStr = isInProgress
                ? '<span class="match-status playing">Playing</span>'
                : '<span class="match-status waiting">Waiting</span>';
            const timerClass = isInProgress ? 'match-timer in-progress' : 'match-timer';

            div.innerHTML = `
                <span class="match-server" style="background-color: ${match.serverColor}20; color: ${match.serverColor}; border-color: ${match.serverColor}">${match.serverName}</span>
                <span class="match-mode">${match.game_mode}</span>
                ${flagsStr ? `<span class="match-flags">${flagsStr}</span>` : ''}
                ${statusStr}
                <span class="${timerClass}">${timerStr}</span>
            `;

            container.appendChild(div);
        }
    }

    formatElapsedTime(seconds) {
        if (seconds < 0) seconds = 0;
        const hours = Math.floor(seconds / 3600);
        const minutes = Math.floor((seconds % 3600) / 60);
        const secs = seconds % 60;

        if (hours > 0) {
            return `${hours}:${minutes.toString().padStart(2, '0')}:${secs.toString().padStart(2, '0')}`;
        }
        return `${minutes}:${secs.toString().padStart(2, '0')}`;
    }

    formatEventType(type) {
        switch (type) {
            case 'created': return 'Match Created';
            case 'started': return 'Match Started';
            case 'completed': return 'Match Completed';
            default: return type;
        }
    }

    formatGameMode(mode) {
        if (mode === 'single_player') return 'Single Player';
        return mode; // "1v1", "2v2", etc.
    }

    formatResult(result, isSinglePlayer) {
        if (isSinglePlayer) {
            // For single player, show Victory/Defeat based on winning team
            // Team 0 = human player, Team 1 = AI
            if (result === 'victory') return 'Victory';
            if (result === 'abandoned') return 'Abandoned';
            return 'Defeat';
        }
        return result;
    }

    formatTime(timestamp) {
        const date = new Date(timestamp);
        return date.toLocaleTimeString('en-US', {
            hour: 'numeric',
            minute: '2-digit',
            hour12: true
        });
    }

    formatDuration(ms) {
        const totalSeconds = Math.floor(ms / 1000);
        const minutes = Math.floor(totalSeconds / 60);
        const seconds = totalSeconds % 60;
        if (minutes > 0) {
            return `${minutes}m ${seconds}s`;
        }
        return `${seconds}s`;
    }

    renderFlags(locations, gameMode) {
        if (!locations || locations.length === 0) return '';

        // Handle single player mode
        if (gameMode === 'single_player') {
            return '<span class="single-player-indicator">Solo</span>';
        }

        // Parse game mode to determine team size (e.g., "2v2" -> 2 players per team)
        const match = gameMode ? gameMode.match(/(\d+)v(\d+)/) : null;
        const teamSize = match ? parseInt(match[1]) : 1;

        // Split participants into teams
        const team1 = locations.slice(0, teamSize);
        const team2 = locations.slice(teamSize);

        const renderPlayer = (loc) => {
            const flag = this.countryCodeToFlag(loc.country_code);
            const countryName = COUNTRY_NAMES[loc.country_code] || loc.country_code;
            const cityStr = loc.city || '';
            const usernameStr = loc.username || '';

            let displayText = flag;
            if (usernameStr) {
                displayText += ` ${usernameStr}`;
            }
            if (cityStr) {
                displayText += `, ${cityStr}`;
            }

            return `<span class="participant-info" title="${countryName}">${displayText}</span>`;
        };

        const renderTeam = (players) => {
            return players.map(renderPlayer).join('<br>');
        };

        // For 1v1, keep it simple on one line
        if (teamSize === 1 && team1.length === 1 && team2.length === 1) {
            return `<span class="teams-container teams-1v1">
                <span class="team">${renderPlayer(team1[0])}</span>
                <span class="vs-divider">vs</span>
                <span class="team">${renderPlayer(team2[0])}</span>
            </span>`;
        }

        // For team games, stack players vertically within each team
        return `<span class="teams-container">
            <span class="team">${renderTeam(team1)}</span>
            <span class="vs-divider">vs</span>
            <span class="team">${renderTeam(team2)}</span>
        </span>`;
    }

    countryCodeToFlag(countryCode) {
        if (!countryCode || countryCode.length !== 2) return '';

        // Convert country code to regional indicator symbols
        const codePoints = countryCode
            .toUpperCase()
            .split('')
            .map(char => 127397 + char.charCodeAt(0));

        return String.fromCodePoint(...codePoints);
    }

    initCharts() {
        Chart.defaults.color = '#9ca3af';
        Chart.defaults.borderColor = '#374151';

        // Queue Size Chart
        const queueCtx = document.getElementById('queueSizeChart').getContext('2d');
        this.queueSizeChart = new Chart(queueCtx, {
            type: 'line',
            data: {
                labels: [],
                datasets: [
                    {
                        label: '1v1',
                        data: [],
                        borderColor: '#22c55e',
                        backgroundColor: 'rgba(34, 197, 94, 0.1)',
                        tension: 0.4,
                        fill: true,
                        borderWidth: 2,
                        pointRadius: 0,
                        pointHoverRadius: 4
                    },
                    {
                        label: '2v2',
                        data: [],
                        borderColor: '#3b82f6',
                        backgroundColor: 'rgba(59, 130, 246, 0.1)',
                        tension: 0.4,
                        fill: true,
                        borderWidth: 2,
                        pointRadius: 0,
                        pointHoverRadius: 4
                    },
                    {
                        label: '3v3',
                        data: [],
                        borderColor: '#f59e0b',
                        backgroundColor: 'rgba(245, 158, 11, 0.1)',
                        tension: 0.4,
                        fill: true,
                        borderWidth: 2,
                        pointRadius: 0,
                        pointHoverRadius: 4
                    },
                    {
                        label: '4v4',
                        data: [],
                        borderColor: '#ec4899',
                        backgroundColor: 'rgba(236, 72, 153, 0.1)',
                        tension: 0.4,
                        fill: true,
                        borderWidth: 2,
                        pointRadius: 0,
                        pointHoverRadius: 4
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: {
                    mode: 'index',
                    intersect: false
                },
                scales: {
                    x: {
                        display: true,
                        grid: {
                            color: 'rgba(55, 65, 81, 0.5)',
                            drawBorder: false
                        },
                        ticks: {
                            maxTicksLimit: 10,
                            color: '#6b7280'
                        }
                    },
                    y: {
                        display: true,
                        beginAtZero: true,
                        grid: {
                            color: 'rgba(55, 65, 81, 0.5)',
                            drawBorder: false
                        },
                        ticks: {
                            color: '#6b7280',
                            stepSize: 1
                        }
                    }
                },
                plugins: {
                    legend: {
                        position: 'top',
                        labels: {
                            usePointStyle: true,
                            padding: 20,
                            color: '#9ca3af'
                        }
                    },
                    tooltip: {
                        backgroundColor: '#1a1a2e',
                        titleColor: '#ffffff',
                        bodyColor: '#9ca3af',
                        borderColor: '#374151',
                        borderWidth: 1,
                        padding: 12,
                        cornerRadius: 8
                    }
                },
                animation: { duration: 0 }
            }
        });

        // Matches Per Minute Chart
        const matchCtx = document.getElementById('matchesPerMinuteChart').getContext('2d');
        this.matchesPerMinuteChart = new Chart(matchCtx, {
            type: 'bar',
            data: {
                labels: [],
                datasets: [{
                    label: 'Matches',
                    data: [],
                    backgroundColor: 'rgba(139, 92, 246, 0.7)',
                    borderColor: '#8b5cf6',
                    borderWidth: 1,
                    borderRadius: 4,
                    hoverBackgroundColor: '#8b5cf6'
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                scales: {
                    x: {
                        display: true,
                        grid: {
                            display: false
                        },
                        ticks: {
                            maxTicksLimit: 12,
                            color: '#6b7280'
                        }
                    },
                    y: {
                        display: true,
                        beginAtZero: true,
                        grid: {
                            color: 'rgba(55, 65, 81, 0.5)',
                            drawBorder: false
                        },
                        ticks: {
                            color: '#6b7280',
                            stepSize: 1
                        }
                    }
                },
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        backgroundColor: '#1a1a2e',
                        titleColor: '#ffffff',
                        bodyColor: '#9ca3af',
                        borderColor: '#374151',
                        borderWidth: 1,
                        padding: 12,
                        cornerRadius: 8
                    }
                },
                animation: { duration: 0 }
            }
        });

        // Weekly Match Chart
        const weeklyCtx = document.getElementById('weeklyMatchChart').getContext('2d');
        this.weeklyMatchChart = new Chart(weeklyCtx, {
            type: 'bar',
            data: {
                labels: [],
                datasets: [{
                    label: 'Matches',
                    data: [],
                    backgroundColor: 'rgba(34, 197, 94, 0.7)',
                    borderColor: '#22c55e',
                    borderWidth: 1,
                    borderRadius: 4,
                    hoverBackgroundColor: '#22c55e'
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                scales: {
                    x: {
                        display: true,
                        grid: {
                            display: false
                        },
                        ticks: {
                            color: '#6b7280'
                        }
                    },
                    y: {
                        display: true,
                        beginAtZero: true,
                        grid: {
                            color: 'rgba(55, 65, 81, 0.5)',
                            drawBorder: false
                        },
                        ticks: {
                            color: '#6b7280',
                            stepSize: 1
                        }
                    }
                },
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        backgroundColor: '#1a1a2e',
                        titleColor: '#ffffff',
                        bodyColor: '#9ca3af',
                        borderColor: '#374151',
                        borderWidth: 1,
                        padding: 12,
                        cornerRadius: 8
                    }
                },
                animation: { duration: 0 }
            }
        });
    }

    updateQueueSizeChart() {
        // For 3 hours of data, show labels in minutes/hours
        const displayLabels = this.aggregatedQueueHistory.map((_, i) => {
            const secondsAgo = (this.aggregatedQueueHistory.length - 1 - i);
            if (i === this.aggregatedQueueHistory.length - 1) return 'now';

            // Show label every 5 minutes (300 seconds)
            if (secondsAgo % 300 === 0) {
                const minutesAgo = secondsAgo / 60;
                if (minutesAgo >= 60) {
                    const hours = Math.floor(minutesAgo / 60);
                    const mins = minutesAgo % 60;
                    return mins > 0 ? `-${hours}h${mins}m` : `-${hours}h`;
                }
                return `-${minutesAgo}m`;
            }
            return '';
        });

        const modes = ['1v1', '2v2', '3v3', '4v4'];
        const datasets = modes.map((mode, index) => {
            return {
                ...this.queueSizeChart.data.datasets[index],
                data: this.aggregatedQueueHistory.map(point => point.sizes[mode] || 0)
            };
        });

        this.queueSizeChart.data.labels = displayLabels;
        this.queueSizeChart.data.datasets = datasets;
        this.queueSizeChart.update('none');
    }

    updateMatchesPerMinuteChart() {
        // Aggregate matches per minute from all servers
        const aggregatedMatches = {};

        for (const serverId of Object.keys(this.serverStates)) {
            const state = this.serverStates[serverId];
            if (state && state.matches_per_minute && this.connectionStatus[serverId]) {
                for (const point of state.matches_per_minute) {
                    if (!aggregatedMatches[point.minute]) {
                        aggregatedMatches[point.minute] = 0;
                    }
                    aggregatedMatches[point.minute] += point.count;
                }
            }
        }

        // Convert to sorted array
        const minutes = Object.keys(aggregatedMatches).map(Number).sort((a, b) => a - b);
        const now = Date.now();

        const labels = minutes.map(minute => {
            const minuteTimestamp = minute * 60 * 1000;
            const minutesAgo = Math.floor((now - minuteTimestamp) / 60000);
            if (minutesAgo === 0) return 'now';
            if (minutesAgo >= 60) {
                const hours = Math.floor(minutesAgo / 60);
                const mins = minutesAgo % 60;
                return mins > 0 ? `-${hours}h${mins}m` : `-${hours}h`;
            }
            return `-${minutesAgo}m`;
        });

        const data = minutes.map(minute => aggregatedMatches[minute]);

        this.matchesPerMinuteChart.data.labels = labels;
        this.matchesPerMinuteChart.data.datasets[0].data = data;
        this.matchesPerMinuteChart.update('none');
    }

    updateWeeklyMatchChart() {
        // Aggregate daily match counts from all servers
        const aggregatedDaily = {};

        for (const serverId of Object.keys(this.serverStates)) {
            const state = this.serverStates[serverId];
            if (state && state.daily_match_counts && this.connectionStatus[serverId]) {
                for (const day of state.daily_match_counts) {
                    if (!aggregatedDaily[day.date]) {
                        aggregatedDaily[day.date] = 0;
                    }
                    aggregatedDaily[day.date] += day.count;
                }
            }
        }

        // Convert to sorted array
        const dates = Object.keys(aggregatedDaily).sort();

        // Format dates for display (e.g., "Mon", "Tue", etc.)
        const labels = dates.map(date => {
            const d = new Date(date + 'T00:00:00');
            return d.toLocaleDateString('en-US', { weekday: 'short' });
        });

        const data = dates.map(date => aggregatedDaily[date]);

        this.weeklyMatchChart.data.labels = labels;
        this.weeklyMatchChart.data.datasets[0].data = data;
        this.weeklyMatchChart.update('none');
    }
}

// Initialize dashboard when DOM is loaded
document.addEventListener('DOMContentLoaded', () => {
    window.dashboard = new MultiServerDashboard();
});
