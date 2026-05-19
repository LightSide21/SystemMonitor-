package com.monitor.Repositories;

import com.monitor.Entities.NotificationSettings;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

@Repository
public interface NotificationSettingsRepository extends JpaRepository<NotificationSettings, Long> {

    // Метод для поиска настроек по ID компьютера
    NotificationSettings findByComputerId(String computerId);
}