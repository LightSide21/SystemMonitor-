package com.monitor.DTO;

import lombok.Data;
import java.util.List;

@Data
public class SystemDataDto {
    private String computerId;
    private String computerName;
    private String timestamp; // C# шлет DateTime, Jackson разберется
    private String configHash;

    private OsDto os;
    private CpuDto cpu;
    private List<GpuDto> gpus;
    private RamDto ram;
    private List<DiskDto> disks;
    private List<NetworkDto> networks;

    // --- Inner Classes matching C# ---

    @Data
    public static class OsDto {
        private String name;
        private String architecture;
        private String userName;
        private String bootTime;
    }

    @Data
    public static class CpuDto {
        private String name;
        private int logicalCores;
        private int physicalCores;
        private int currentFrequency;
        private int maxFrequency;
        private int load;
        private String temperature; // "45 °C"
        private int processCount;
        private String power;
    }

    @Data
    public static class GpuDto {
        private String name;
        private String vendor;
        private String memory;
        private String utilization; // "45 %"
        private String temperature; // "50 °C"
        private String power;
    }

    @Data
    public static class RamDto {
        private String total;
        private String used;
        private String available;
        private String load; // "45 %"
    }

    @Data
    public static class DiskDto {
        private String model;
        private String totalCapacity;
        private String temperature;
        private List<PartitionDto> partitions;
    }

    @Data
    public static class PartitionDto {
        private String driveLetter;
        private String totalSpace;
        private String freeSpace;
    }

    @Data
    public static class NetworkDto {
        private String name;
        private String adapterName;
        private String ipv4;
        private String sendSpeed;
        private String receiveSpeed;
    }
}