                    ┌─────────────────────┐
                    │       Backend       │
                    │      FastAPI        │
                    └──────────┬──────────┘
                               │
                        HTTPS / REST API
                               │
                               ▼
                    ┌─────────────────────┐
                    │   Desktop Agent     │
                    │                     │
                    │ Monitoring Engine    │
                    │        │            │
                    │   Collectors         │
                    │        │            │
                    │   Local Storage      │
                    │        │            │
                    │   Sync Service       │
                    └──────────┬──────────┘
                               │
                               ▼
                         Windows PC