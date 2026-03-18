use maxminddb::{geoip2, Reader};
use std::net::IpAddr;
use std::path::Path;
use std::sync::Arc;

pub struct GeoLocationService {
    reader: Option<Reader<Vec<u8>>>,
}

impl GeoLocationService {
    pub fn new() -> Arc<Self> {
        let db_path = Path::new("data/GeoLite2-City.mmdb");

        let reader = if db_path.exists() {
            match Reader::open_readfile(db_path) {
                Ok(r) => {
                    tracing::info!("GeoLite2 database loaded successfully from {:?}", db_path);
                    Some(r)
                }
                Err(e) => {
                    tracing::warn!("Failed to load GeoLite2 database: {}. IP geolocation will be disabled.", e);
                    None
                }
            }
        } else {
            tracing::info!(
                "GeoLite2 database not found at {:?}. IP geolocation will be disabled. \
                Download from MaxMind and place in backend/data/GeoLite2-City.mmdb",
                db_path
            );
            None
        };

        Arc::new(Self { reader })
    }

    pub fn lookup_country(&self, ip: &str) -> Option<(String, Option<String>)> {
        let ip_addr: IpAddr = ip.parse().ok()?;

        // For private/local IPs, return a random test country code with test city
        if is_private_ip(&ip_addr) {
            let test_locations = [
                ("US", Some("New York")),
                ("GB", Some("London")),
                ("DE", Some("Berlin")),
                ("FR", Some("Paris")),
                ("JP", Some("Tokyo")),
                ("AU", Some("Sydney")),
                ("CA", Some("Toronto")),
                ("BR", Some("São Paulo")),
                ("KR", Some("Seoul")),
                ("IN", Some("Mumbai")),
            ];
            let index = std::time::SystemTime::now()
                .duration_since(std::time::UNIX_EPOCH)
                .map(|d| (d.as_nanos() % test_locations.len() as u128) as usize)
                .unwrap_or(0);
            let (country, city) = test_locations[index];
            return Some((country.to_string(), city.map(|s| s.to_string())));
        }

        let reader = self.reader.as_ref()?;
        let city_result: geoip2::City = reader.lookup(ip_addr).ok()?;
        let country_code = city_result.country?.iso_code?.to_string();
        let city_name = city_result.city.and_then(|c| c.names).and_then(|n| n.get("en").map(|s| s.to_string()));
        Some((country_code, city_name))
    }

    pub fn is_available(&self) -> bool {
        self.reader.is_some()
    }
}

fn is_private_ip(ip: &IpAddr) -> bool {
    match ip {
        IpAddr::V4(ipv4) => {
            ipv4.is_loopback()
                || ipv4.is_private()
                || ipv4.is_link_local()
                || ipv4.is_broadcast()
                || ipv4.is_unspecified()
        }
        IpAddr::V6(ipv6) => ipv6.is_loopback() || ipv6.is_unspecified(),
    }
}

impl Default for GeoLocationService {
    fn default() -> Self {
        Self { reader: None }
    }
}
