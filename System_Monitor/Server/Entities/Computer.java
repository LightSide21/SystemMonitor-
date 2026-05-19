package com.monitor.Entities;

import jakarta.persistence.*;
import lombok.Data;
import java.time.LocalDateTime;

@Entity
@Table(name = "computers")
@Data
public class Computer {
    @Id
    private String id; // Например: ABC-123

    private String name;

    private Long userId; // владелец

    // --- ДЛЯ КОМПЛЕКТУЮЩИХ ПК ---
    private String cpuName;      // Процессор
    private String ramTotal;     // Объем ОЗУ
    private String gpuNames;     // Видеокарты

    private String osName;           // ОС
    private Integer logicalCores;    // логические ядра
    private Integer physicalCores;

    @Column(length = 500)
    private String storageInfo;  // Диски
    // ------------------------------------------

//    @Column(columnDefinition = "TEXT")
//    private String hardwareSpecs;

    private String configHash; // Хеш конфигурации

    private boolean isOnline;

    private LocalDateTime lastSeen;

    @Column(columnDefinition = "TEXT")
    private String processesSnapshot;
}