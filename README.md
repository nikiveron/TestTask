# Инструкция по запуску проекта

## 1. Добавить .env в корень проекта

пример файла с полным списком переменных окружения: 
```
# Database Configuration
POSTGRES_USER=postgres
POSTGRES_PASSWORD=postgres
POSTGRES_DB=testapp_db
POSTGRES_PORT=5432

PGADMIN_DEFAULT_EMAIL=postgres@localhost.com
PGADMIN_DEFAULT_PASSWORD=postgres

# RabbitMQ Configuration
RABBITMQ_USER=guest
RABBITMQ_PASSWORD=guest
RABBITMQ_PORT=5672
RABBITMQ_MANAGEMENT_PORT=15672

# Application Configuration
ASPNETCORE_ENVIRONMENT=Development
APP_PORT=8080
```

## 2. Запустить проект с помощью Docker Compose

```
docker-compose build
docker-compose up -d
```

