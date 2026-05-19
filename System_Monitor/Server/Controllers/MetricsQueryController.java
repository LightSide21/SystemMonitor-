package com.monitor.Controllers;

import com.monitor.Services.InfluxService;
import lombok.RequiredArgsConstructor;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.*;

import java.util.Map;

@RestController
@RequestMapping("/metrics")
@RequiredArgsConstructor
public class MetricsQueryController {

    private final InfluxService influxService;

    @GetMapping("/dashboard")
    public ResponseEntity<Map<String, Object>> getDashboard(
            @RequestParam String computerId,
            @RequestParam(defaultValue = "24h") String timeRange) { // Добавили параметр
        return ResponseEntity.ok(influxService.getComputerMetrics(computerId, timeRange));
    }
}