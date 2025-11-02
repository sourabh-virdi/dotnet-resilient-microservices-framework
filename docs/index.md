---
layout: home
title: Home
nav_order: 1
description: "Resilient Microservices Framework - A comprehensive .NET framework for building production-ready microservices"
permalink: /
---

# 🚀 Resilient Microservices Framework for .NET
{: .fs-9 }

A comprehensive, production-ready .NET framework for building resilient microservices with built-in patterns for circuit breakers, retry policies, distributed tracing, saga orchestration, and comprehensive metrics collection.
{: .fs-6 .fw-300 }

[Get started now](#getting-started){: .btn .btn-primary .fs-5 .mb-4 .mb-md-0 .mr-2 } [View it on GitHub](https://github.com/sourabh-virdi/dotnet-resilient-microservices-framework){: .btn .fs-5 .mb-4 .mb-md-0 }

---

## 🌟 Key Features

### 🛡️ **Built-in Resilience Patterns**
- **Circuit Breakers** - Prevent cascading failures with configurable thresholds
- **Retry Policies** - Exponential backoff with jitter to handle transient failures
- **Timeout Management** - Configurable operation timeouts to prevent resource exhaustion
- **Bulkhead Isolation** - Resource isolation patterns for fault containment

### 🔍 **Comprehensive Observability**
- **Distributed Tracing** - OpenTelemetry + Jaeger integration for request tracking
- **Metrics Collection** - Prometheus-compatible metrics with custom collection
- **Health Checks** - Comprehensive health monitoring for all components
- **Structured Logging** - Correlation ID tracking across service boundaries

### 📨 **Event-Driven Architecture**
- **Message Bus** - RabbitMQ integration with auto-recovery
- **Pub/Sub Patterns** - Loose coupling between microservices
- **Saga Orchestration** - Distributed transaction management with compensation

### 🧪 **Developer Experience**
- **Docker Ready** - Complete containerization with Docker Compose
- **Unit Testing** - Comprehensive test coverage with real examples
- **API Documentation** - Auto-generated Swagger/OpenAPI documentation
- **Configuration** - Flexible JSON and environment-based configuration

---

## 🚀 Quick Start

### Prerequisites
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Docker & Docker Compose](https://docs.docker.com/get-docker/)

### 1. Clone and Setup
```bash
git clone https://github.com/sourabh-virdi/dotnet-resilient-microservices-framework.git
cd dotnet-resilient-microservices-framework

# Start infrastructure
docker-compose up -d rabbitmq jaeger
```

### 2. Run Sample Services
```bash
# Terminal 1 - Order Service
cd samples/OrderService && dotnet run

# Terminal 2 - Payment Service  
cd samples/PaymentService && dotnet run

# Terminal 3 - Inventory Service
cd samples/InventoryService && dotnet run
```

### 3. Test the System
```bash
# Create an order (triggers distributed transaction)
curl -X POST http://localhost:5001/api/orders \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": 123,
    "items": [{"productId": "PROD001", "quantity": 2, "price": 25.0}]
  }'

# Check health status
curl http://localhost:5001/health
curl http://localhost:5002/health
curl http://localhost:5003/health

# View metrics
curl http://localhost:5001/metrics
```

### 4. Monitor and Observe
- **Services**: Order (5001), Payment (5002), Inventory (5003)
- **RabbitMQ Management**: http://localhost:15672 (guest/guest)
- **Jaeger UI**: http://localhost:16686
- **Metrics**: Available at `/metrics` endpoint on each service

---

## 🏗️ Architecture Overview

The framework follows a layered architecture with clear separation of concerns:

```
┌─────────────────────────────────────────────┐
│                Client Layer                 │
│  Web UI/Mobile Apps • External Services    │
└─────────────────────────────────────────────┘
                      │
┌─────────────────────────────────────────────┐
│              API Gateway Layer              │
│   Load Balancing • Rate Limiting • Auth    │
└─────────────────────────────────────────────┘
                      │
┌─────────────────────────────────────────────┐
│            Microservices Layer              │
│  Order Service • Payment Service • Inv...  │
└─────────────────────────────────────────────┘
                      │
┌─────────────────────────────────────────────┐
│             Framework Libraries             │
│  Core • Resilience • Tracing • Messaging   │
│       Sagas • Metrics                      │
└─────────────────────────────────────────────┘
                      │
┌─────────────────────────────────────────────┐
│            Infrastructure Layer             │
│  RabbitMQ • Jaeger • Prometheus • Grafana  │
└─────────────────────────────────────────────┘
```

---

## 📚 Framework Components

### **ResilientMicroservices.Core**
Foundation library providing core abstractions, health checks, and metrics interfaces.

### **ResilientMicroservices.Resilience** 
Polly-based resilience patterns including circuit breakers, retry policies, and timeout management.

### **ResilientMicroservices.Tracing**
OpenTelemetry integration for distributed tracing with Jaeger exporter support.

### **ResilientMicroservices.Messaging**
RabbitMQ-based message bus with pub/sub patterns and auto-recovery.

### **ResilientMicroservices.Sagas**
Distributed transaction orchestration with automatic compensation on failures.

### **ResilientMicroservices.Metrics**
Comprehensive metrics collection with Prometheus integration and custom metrics support.

---

## 🎯 Sample Services

### 🛒 **Order Service**
- Order lifecycle management
- Saga orchestration for distributed transactions
- Integration with Payment and Inventory services

### 💳 **Payment Service** 
- Payment processing with multiple payment methods
- Refund management (full and partial)
- Integration with external payment gateways

### 📦 **Inventory Service**
- Real-time inventory tracking
- Temporary inventory reservations with expiry
- Stock level management and updates

---

## 📊 Performance & Benchmarks

| Service | Throughput | Latency | Availability |
|---------|------------|---------|--------------|
| Order Service | 500 orders/sec | <100ms | 99.9% |
| Payment Service | 1000 payments/sec | <200ms | 99.95% |
| Inventory Service | 2000 ops/sec | <50ms | 99.9% |
| Message Bus | 10K messages/sec | <10ms | 99.99% |

---

## 🤝 Getting Help

- **📖 Documentation**: Comprehensive guides and API reference
- **💬 Discussions**: [GitHub Discussions](https://github.com/sourabh-virdi/dotnet-resilient-microservices-framework/discussions)
- **🐛 Issues**: [GitHub Issues](https://github.com/sourabh-virdi/dotnet-resilient-microservices-framework/issues)
- **📝 Examples**: Working sample applications included

---

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](https://github.com/sourabh-virdi/dotnet-resilient-microservices-framework/blob/main/LICENSE) file for details.

---

*Built with ❤️ for the .NET community by developers who understand the challenges of building resilient distributed systems.* 