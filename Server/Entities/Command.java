package com.monitor.Entities;

import jakarta.persistence.*;
import lombok.Data;
import java.time.LocalDateTime;

@Entity
@Table(name = "commands")
@Data
public class Command {
    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    private String computerId;

    private String action; // Типы: SHUTDOWN, REBOOT, KILL_PROCESS

    private String payload; // имя процесса "chrome"

    private String status; // PENDING, DELIVERED, EXECUTED

    private LocalDateTime createdAt = LocalDateTime.now();
}
