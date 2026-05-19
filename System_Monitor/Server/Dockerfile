# Этап сборки (сборка .jar файла из исходников)
FROM maven:3.9.9-eclipse-temurin-21 AS build
WORKDIR /app
COPY pom.xml .
COPY src ./src
# Пропускаем тесты для ускорения первой сборки
RUN mvn clean package -DskipTests

# Этап запуска
FROM eclipse-temurin:21-jre-alpine
WORKDIR /app
# Копируем собранный jar из предыдущего этапа
COPY --from=build /app/target/*.jar app.jar
# Открываем порт, указанный в ваших настройках
EXPOSE 8045
ENTRYPOINT ["java", "-jar", "app.jar"]