#!/bin/bash

# Test the matchmaking server
BASE_URL="http://localhost:8080"

echo "=== Running Unit Tests ==="
cargo test
if [ $? -ne 0 ]; then
    echo "Unit tests failed!"
    exit 1
fi

echo -e "\n=== Testing Health ==="
curl -s "$BASE_URL/health" | jq .

echo -e "\n=== Login Player 1 ==="
P1=$(curl -s -X POST "$BASE_URL/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"username": "Player1"}')
echo "$P1" | jq .
TOKEN1=$(echo "$P1" | jq -r '.token')

echo -e "\n=== Login Player 2 ==="
P2=$(curl -s -X POST "$BASE_URL/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"username": "Player2"}')
echo "$P2" | jq .
TOKEN2=$(echo "$P2" | jq -r '.token')

echo -e "\n=== Join Queue (Player 1) ==="
curl -s -X POST "$BASE_URL/api/queue/join" \
  -H "Content-Type: application/json" \
  -d "{\"token\": \"$TOKEN1\", \"game_mode\": \"OneVsOne\"}" | jq .

echo -e "\n=== Join Queue (Player 2) ==="
curl -s -X POST "$BASE_URL/api/queue/join" \
  -H "Content-Type: application/json" \
  -d "{\"token\": \"$TOKEN2\", \"game_mode\": \"OneVsOne\"}" | jq .

echo -e "\n=== Test Complete ==="
echo "Both players joined queue. Connect via WebSocket to see match notifications."
echo ""
echo "To test WebSocket, run:"
echo "  wscat -c ws://localhost:8080/ws"
echo "Then send:"
echo "  {\"type\":\"Authenticate\",\"data\":{\"token\":\"$TOKEN1\"}}"
