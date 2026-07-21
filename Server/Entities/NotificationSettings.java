package com.monitor.Entities;

import jakarta.persistence.*;
import lombok.Data;

@Entity
@Table(name = "notification_settings")
@Data
public class NotificationSettings {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(name = "computer_id", nullable = false, unique = true)
    private String computerId;

    @Column(name = "user_id", nullable = false)
    private Long userId;

    // --- CPU ---
    @Column(name = "cpu_temp_enabled")
    private boolean cpuTempEnabled;

    @Column(name = "cpu_temp_threshold")
    private double cpuTempThreshold;

    // --- GPU ---
    @Column(name = "gpu_temp_enabled")
    private boolean gpuTempEnabled;

    @Column(name = "gpu_temp_threshold")
    private double gpuTempThreshold;

    // --- DISKS ---
    @Column(name = "disk_temp_enabled")
    private boolean diskTempEnabled;

    @Column(name = "disk_temp_threshold")
    private double diskTempThreshold;
}