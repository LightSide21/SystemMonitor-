package com.monitor;

import org.springframework.boot.SpringApplication;
import org.springframework.boot.autoconfigure.SpringBootApplication;
import org.springframework.context.annotation.Bean;
import org.springframework.scheduling.annotation.EnableScheduling;
import tools.jackson.databind.ObjectMapper;

@SpringBootApplication
@EnableScheduling 
public class SystemMonitorApplication {
    public static void main(String[] args) {
        SpringApplication.run(SystemMonitorApplication.class, args);
    }
}
