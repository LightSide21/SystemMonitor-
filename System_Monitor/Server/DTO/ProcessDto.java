package com.monitor.DTO;

import lombok.Data;

@Data
public class ProcessDto {
    private int id;           // PID процесса (нужен для команды KILL_PROCESS)
    private String name;      // Имя процесса (например, "chrome")
    private long memoryBytes; // Потребление оперативной памяти
    private String memoryFormatted;
    private String status;
}
