package com.monitor.Controllers;

import com.monitor.DTO.ProcessDto;
import com.monitor.DTO.SystemDataDto;
import com.monitor.DTO.CommandDto;
import com.monitor.Entities.Command;
import com.monitor.Repositories.CommandRepository;
import com.monitor.Repositories.ComputerRepository;
import com.monitor.Services.AgentService;
import com.monitor.Services.InfluxService;
import lombok.RequiredArgsConstructor;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.*;

@RestController
@RequestMapping("/ingest")
@RequiredArgsConstructor
public class IngestionController {

    private final AgentService agentService;
    private final InfluxService influxService;
    private final CommandRepository commandRepository;

    private final ComputerRepository computerRepository;

    @PostMapping
    public ResponseEntity<CommandDto> ingestData(@RequestBody SystemDataDto data) {
        // 1. Сохраняем метрики
        agentService.processIncomingData(data);
        influxService.writeMetrics(data);

        // 2. Проверяем, есть ли команды для этого ПК
        return commandRepository.findFirstByComputerIdAndStatusOrderByCreatedAtAsc(data.getComputerId(), "PENDING")
                .map(cmd -> {
                    // Команда найдена, Меняем статус на "Доставлено"
                    cmd.setStatus("DELIVERED");
                    commandRepository.save(cmd);
                    // Отправляем агенту
                    return ResponseEntity.ok(new CommandDto(cmd.getId(), cmd.getAction(), cmd.getPayload()));
                })
                .orElseGet(() -> ResponseEntity.ok().build()); // Если команд нет - возвращаем пустой
    }

    // 3. Эндпоинт, куда агент сообщит, что он выполнил команду
    @PostMapping("/command/{id}/status")
    public ResponseEntity<?> updateCommandStatus(@PathVariable Long id, @RequestParam String status) {
        commandRepository.findById(id).ifPresent(cmd -> {
            cmd.setStatus(status); // Установит EXECUTED
            commandRepository.save(cmd);
        });
        return ResponseEntity.ok().build();
    }

    @PostMapping("/processes/{computerId}")
    public ResponseEntity<?> receiveProcesses(@PathVariable String computerId, @RequestBody java.util.List<ProcessDto> processes) {
        computerRepository.findById(computerId).ifPresent(computer -> {
            try {
                // Превращаем список процессов в JSON строку и сохраняем в БД
                String jsonSnapshot = new tools.jackson.databind.ObjectMapper().writeValueAsString(processes);
                computer.setProcessesSnapshot(jsonSnapshot);
                computerRepository.save(computer);
            } catch (Exception e) {
                e.printStackTrace();
            }
        });
        return ResponseEntity.ok().build();
    }
}