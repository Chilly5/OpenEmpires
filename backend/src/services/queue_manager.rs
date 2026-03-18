use std::collections::{HashMap, VecDeque};
use std::sync::Arc;
use tokio::sync::RwLock;
use uuid::Uuid;

use crate::models::{GameMode, QueueEntry};

pub struct QueueManager {
    queues: RwLock<HashMap<GameMode, VecDeque<QueueEntry>>>,
}

impl QueueManager {
    pub fn new() -> Arc<Self> {
        let mut queues = HashMap::new();
        queues.insert(GameMode::OneVsOne, VecDeque::new());
        queues.insert(GameMode::TwoVsTwo, VecDeque::new());
        queues.insert(GameMode::ThreeVsThree, VecDeque::new());
        queues.insert(GameMode::FourVsFour, VecDeque::new());

        Arc::new(Self {
            queues: RwLock::new(queues),
        })
    }

    pub async fn add_to_queue(&self, entry: QueueEntry) -> usize {
        let mut queues = self.queues.write().await;
        let queue = queues.get_mut(&entry.game_mode).unwrap();
        queue.push_back(entry);
        queue.len()
    }

    pub async fn remove_from_queue(&self, player_id: Uuid) -> Option<GameMode> {
        let mut queues = self.queues.write().await;
        for (mode, queue) in queues.iter_mut() {
            if let Some(pos) = queue.iter().position(|e| e.player_id == player_id) {
                queue.remove(pos);
                return Some(*mode);
            }
        }
        None
    }

    pub async fn get_position(&self, player_id: Uuid) -> Option<(GameMode, usize)> {
        let queues = self.queues.read().await;
        for (mode, queue) in queues.iter() {
            if let Some(pos) = queue.iter().position(|e| e.player_id == player_id) {
                return Some((*mode, pos + 1));
            }
        }
        None
    }

    pub async fn try_match(&self, game_mode: GameMode) -> Option<Vec<QueueEntry>> {
        let mut queues = self.queues.write().await;
        let queue = queues.get_mut(&game_mode)?;
        let required = game_mode.players_required();

        // Full match: take immediately
        if queue.len() >= required {
            return Some(queue.drain(..required).collect());
        }

        // Partial match: need >= 2, oldest must have waited >= 5s
        if queue.len() >= 2 {
            if let Some(oldest) = queue.front() {
                if oldest.joined_at.elapsed() >= std::time::Duration::from_secs(5) {
                    let count = queue.len().min(required);
                    return Some(queue.drain(..count).collect());
                }
            }
        }

        None
    }

    pub async fn dequeue_one(&self, game_mode: GameMode) -> Option<QueueEntry> {
        let mut queues = self.queues.write().await;
        let queue = queues.get_mut(&game_mode)?;
        queue.pop_front()
    }

    pub async fn get_queue_sizes(&self) -> HashMap<GameMode, usize> {
        let queues = self.queues.read().await;
        queues.iter().map(|(m, q)| (*m, q.len())).collect()
    }

    pub async fn get_queue_entries(&self) -> HashMap<GameMode, Vec<QueueEntry>> {
        let queues = self.queues.read().await;
        queues
            .iter()
            .map(|(mode, queue)| (*mode, queue.iter().cloned().collect()))
            .collect()
    }
}

impl Default for QueueManager {
    fn default() -> Self {
        let mut queues = HashMap::new();
        queues.insert(GameMode::OneVsOne, VecDeque::new());
        queues.insert(GameMode::TwoVsTwo, VecDeque::new());
        queues.insert(GameMode::ThreeVsThree, VecDeque::new());
        queues.insert(GameMode::FourVsFour, VecDeque::new());

        Self {
            queues: RwLock::new(queues),
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn create_entry(game_mode: GameMode) -> QueueEntry {
        QueueEntry::new(Uuid::new_v4(), "TestPlayer".to_string(), game_mode, 0)
    }

    #[tokio::test]
    async fn test_add_to_queue() {
        let manager = QueueManager::new();
        let entry = create_entry(GameMode::OneVsOne);
        let position = manager.add_to_queue(entry).await;
        assert_eq!(position, 1);
    }

    #[tokio::test]
    async fn test_add_multiple_players() {
        let manager = QueueManager::new();

        for i in 1..=4 {
            let entry = QueueEntry::new(
                Uuid::new_v4(),
                format!("Player{}", i),
                GameMode::TwoVsTwo,
                0,
            );
            let position = manager.add_to_queue(entry).await;
            assert_eq!(position, i);
        }
    }

    #[tokio::test]
    async fn test_remove_from_queue() {
        let manager = QueueManager::new();
        let player_id = Uuid::new_v4();
        let entry = QueueEntry::new(player_id, "TestPlayer".to_string(), GameMode::OneVsOne, 0);

        manager.add_to_queue(entry).await;
        let removed_mode = manager.remove_from_queue(player_id).await;
        assert_eq!(removed_mode, Some(GameMode::OneVsOne));

        let position = manager.get_position(player_id).await;
        assert!(position.is_none());
    }

    #[tokio::test]
    async fn test_empty_queue_try_match() {
        let manager = QueueManager::new();
        let result = manager.try_match(GameMode::OneVsOne).await;
        assert!(result.is_none());
    }

    #[tokio::test]
    async fn test_single_player_no_partial_match() {
        // A single player in 2v2 should never partially match (need >= 2)
        let manager = QueueManager::new();
        let entry = create_entry(GameMode::TwoVsTwo);
        manager.add_to_queue(entry).await;

        let result = manager.try_match(GameMode::TwoVsTwo).await;
        assert!(result.is_none());
    }

    #[tokio::test]
    async fn test_partial_match_before_grace_period() {
        // 2 players in 2v2 should NOT match before grace period
        let manager = QueueManager::new();
        manager.add_to_queue(create_entry(GameMode::TwoVsTwo)).await;
        manager.add_to_queue(create_entry(GameMode::TwoVsTwo)).await;

        let result = manager.try_match(GameMode::TwoVsTwo).await;
        assert!(result.is_none());
    }

    #[tokio::test]
    async fn test_partial_match_after_grace_period() {
        // 2 players in 2v2 should match after 5s grace period
        let manager = QueueManager::new();

        // Create entries with backdated joined_at
        let entry1 = QueueEntry {
            player_id: Uuid::new_v4(),
            username: "Player1".to_string(),
            game_mode: GameMode::TwoVsTwo,
            joined_at: std::time::Instant::now() - std::time::Duration::from_secs(6),
        };
        let entry2 = QueueEntry {
            player_id: Uuid::new_v4(),
            username: "Player2".to_string(),
            game_mode: GameMode::TwoVsTwo,
            joined_at: std::time::Instant::now() - std::time::Duration::from_secs(3),
        };
        manager.add_to_queue(entry1).await;
        manager.add_to_queue(entry2).await;

        let result = manager.try_match(GameMode::TwoVsTwo).await;
        assert!(result.is_some());
        assert_eq!(result.unwrap().len(), 2);
    }

    #[tokio::test]
    async fn test_1v1_match() {
        let manager = QueueManager::new();

        for _ in 0..2 {
            let entry = create_entry(GameMode::OneVsOne);
            manager.add_to_queue(entry).await;
        }

        let result = manager.try_match(GameMode::OneVsOne).await;
        assert!(result.is_some());
        assert_eq!(result.unwrap().len(), 2);
    }

    #[tokio::test]
    async fn test_2v2_match() {
        let manager = QueueManager::new();

        for _ in 0..4 {
            let entry = create_entry(GameMode::TwoVsTwo);
            manager.add_to_queue(entry).await;
        }

        let result = manager.try_match(GameMode::TwoVsTwo).await;
        assert!(result.is_some());
        assert_eq!(result.unwrap().len(), 4);
    }

    #[tokio::test]
    async fn test_3v3_match() {
        let manager = QueueManager::new();

        for _ in 0..6 {
            let entry = create_entry(GameMode::ThreeVsThree);
            manager.add_to_queue(entry).await;
        }

        let result = manager.try_match(GameMode::ThreeVsThree).await;
        assert!(result.is_some());
        assert_eq!(result.unwrap().len(), 6);
    }

    #[tokio::test]
    async fn test_4v4_match() {
        let manager = QueueManager::new();

        for _ in 0..8 {
            let entry = create_entry(GameMode::FourVsFour);
            manager.add_to_queue(entry).await;
        }

        let result = manager.try_match(GameMode::FourVsFour).await;
        assert!(result.is_some());
        assert_eq!(result.unwrap().len(), 8);
    }

    #[tokio::test]
    async fn test_dequeue_one() {
        let manager = QueueManager::new();
        let entry1 = QueueEntry::new(Uuid::new_v4(), "Player1".to_string(), GameMode::TwoVsTwo, 0);
        let entry2 = QueueEntry::new(Uuid::new_v4(), "Player2".to_string(), GameMode::TwoVsTwo, 0);
        let p1_id = entry1.player_id;

        manager.add_to_queue(entry1).await;
        manager.add_to_queue(entry2).await;

        let dequeued = manager.dequeue_one(GameMode::TwoVsTwo).await;
        assert!(dequeued.is_some());
        assert_eq!(dequeued.unwrap().player_id, p1_id); // FIFO order

        let sizes = manager.get_queue_sizes().await;
        assert_eq!(sizes.get(&GameMode::TwoVsTwo), Some(&1));
    }

    #[tokio::test]
    async fn test_dequeue_one_empty() {
        let manager = QueueManager::new();
        let dequeued = manager.dequeue_one(GameMode::TwoVsTwo).await;
        assert!(dequeued.is_none());
    }

    #[tokio::test]
    async fn test_queue_sizes() {
        let manager = QueueManager::new();

        // Add 2 to 1v1
        for _ in 0..2 {
            manager.add_to_queue(create_entry(GameMode::OneVsOne)).await;
        }
        // Add 3 to 2v2
        for _ in 0..3 {
            manager.add_to_queue(create_entry(GameMode::TwoVsTwo)).await;
        }
        // Add 1 to 3v3
        manager.add_to_queue(create_entry(GameMode::ThreeVsThree)).await;

        let sizes = manager.get_queue_sizes().await;
        assert_eq!(sizes.get(&GameMode::OneVsOne), Some(&2));
        assert_eq!(sizes.get(&GameMode::TwoVsTwo), Some(&3));
        assert_eq!(sizes.get(&GameMode::ThreeVsThree), Some(&1));
        assert_eq!(sizes.get(&GameMode::FourVsFour), Some(&0));
    }
}
