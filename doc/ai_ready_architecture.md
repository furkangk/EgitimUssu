# 🧠 AI-READY ARCHITECTURE DOCUMENT  
## Özel Ders Platformu (Tutoring Platform)

---

# 🎯 PURPOSE

This document describes the **full system architecture** of a mobile-first tutoring platform.  
It is written to be **machine-readable and AI-friendly**, enabling:

- Feature generation
- API design
- Code scaffolding
- System evolution (monolith → microservices)

---

# 🧩 SYSTEM OVERVIEW

## System Type
- Modular Monolith (evolvable to Microservices)

## Platforms
- Mobile (Primary)
- Web (Secondary - future)
- Backend API (Core system)

## Core Actors
- Teacher
- Student
- Parent
- Admin

---

# 🏗️ HIGH-LEVEL ARCHITECTURE

```
Client Layer
 ├── Mobile (Flutter)
 └── Web (Angular - future)

Backend Layer (.NET 8)
 ├── API Host
 ├── Modules
 └── Shared Core

Infrastructure
 ├── PostgreSQL
 ├── Redis
 ├── (Future: RabbitMQ)
```

---

# 🧱 BACKEND ARCHITECTURE

## Architectural Style
- Clean Architecture
- Domain Driven Design (DDD)
- CQRS
- Event-Driven (lightweight)
- Outbox Pattern

---

## 📦 MODULE STRUCTURE

Each module follows this structure:

```
ModuleName/
 ├── API
 ├── Application
 ├── Domain
 └── Infrastructure
```

---

## 🔑 CORE MODULES (MAPPED TO PRODUCT)

### AUTH MODULE
Handles:
- Authentication
- Authorization
- Role management

---

### USER DOMAIN

#### Teacher
- Profile
- Availability
- Pricing

#### Student
- Can exist independently
- Can be linked to teacher

#### Parent
- Observes student data

---

### LESSON MODULE
- Schedule lessons
- Track sessions
- Store lesson outcomes

---

### STUDY MODULE
- Timer-based study sessions
- Test tracking
- Performance metrics
- Streak system

---

### MATCHING MODULE (Growth Engine)
- Teacher discovery
- Filtering
- Requests
- Messaging

---

### NOTIFICATION MODULE
- Push notifications (FCM)
- Event-triggered messaging
- Future: SMS / WhatsApp

---

### PAYMENT MODULE
- Manual tracking (initial)
- Future: subscription system

---

# 🔁 DATA FLOW

Standard request flow:

1. Client → API
2. Controller → Application Layer
3. Command/Query → Handler
4. Domain Logic executes
5. Event produced (optional)
6. Data persisted
7. Event dispatched (Outbox)

---

# ⚡ EVENT SYSTEM

Example events:

- LessonCompletedEvent
- StudySessionEndedEvent
- StudentCreatedEvent
- NotificationTriggeredEvent

Purpose:
- Decoupling modules
- Future async processing

---

# 🧠 DOMAIN PRINCIPLES

- Each module owns its data
- No direct cross-module DB access
- Communication via:
  - Application services
  - Domain events

---

# 🧰 SHARED LAYER

## Kernel
- BaseEntity
- DomainEvent
- Result pattern

## Common
- Exceptions
- Extensions
- Pagination

## Infrastructure
- DbContext
- Redis
- Middleware
- Outbox

---

# 📱 MOBILE ARCHITECTURE

## Approach
- Feature-based
- Clean Architecture

## Structure
```
core/
features/
shared/
```

## Features map 1:1 with backend:
- auth
- lesson
- study
- matching
- payment
- notification

---

# 🌐 WEB ARCHITECTURE

## Status
Planned (Phase later)

## Structure
```
core/
features/
shared/
```

---

# 🗄️ DATA LAYER

## PostgreSQL
Primary storage:
- Users
- Lessons
- Study sessions
- Payments
- Reviews

## Redis
Used for:
- Cache
- Counters
- Performance optimization

---

# 🚀 SCALABILITY STRATEGY

## Current
- Modular Monolith

## Future Evolution

Modules can be extracted into services:

- Matching Service
- Notification Service
- Payment Service

---

# 📊 PHASE ALIGNMENT

## Phase 0
- Auth
- Core infra

## Phase 1
- Teacher workflow (MVP)

## Phase 2
- Student independent usage

## Phase 3
- Analytics & notifications

## Phase 4
- Matching system

## Phase 5
- Monetization (premium)

---

# 🤖 AI USAGE GUIDE

This architecture is optimized for AI usage.

## When generating features:
- Always map to a module
- Respect layer separation
- Use CQRS pattern

## When generating APIs:
- Use Controller → Command/Query → Handler
- Return Result pattern

## When generating DB:
- Respect module boundaries
- Avoid shared tables

---

# 🎯 DESIGN GOALS

- Mobile-first
- Daily usage habit creation
- Strong domain boundaries
- Scalable growth system
- Monetization-ready

---

# 🧾 SUMMARY

This system is:

- Modular
- Scalable
- Domain-driven
- AI-friendly
- Ready for future microservices

---

👉 This document is designed to be directly usable by AI systems for:
- feature generation
- backend scaffolding
- system evolution
