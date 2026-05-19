package com.monitor.Controllers;

import com.monitor.Entities.Command;
import com.monitor.Entities.Computer;
import com.monitor.Entities.NotificationSettings;
import com.monitor.Entities.User;
import com.monitor.Repositories.CommandRepository;
import com.monitor.Repositories.ComputerRepository;
import com.monitor.Repositories.UserRepository;
import com.monitor.Repositories.NotificationSettingsRepository;
import lombok.RequiredArgsConstructor;
import org.springframework.http.ResponseEntity;
import org.springframework.security.core.context.SecurityContextHolder;
import org.springframework.web.bind.annotation.*;

import java.util.Optional;

@RestController
@RequestMapping("/manage")
@RequiredArgsConstructor
public class DeviceManagementController {

    private final CommandRepository commandRepository;
    private final ComputerRepository computerRepository;
    private final UserRepository userRepository;
    private final NotificationSettingsRepository settingsRepository;


    private User getCurrentUser() {
        String username = SecurityContextHolder.getContext().getAuthentication().getName();
        return userRepository.findByUsername(username)
                .orElseThrow(() -> new RuntimeException("Пользователь не найден"));
    }

    @PostMapping("/command")
    public ResponseEntity<?> sendCommand(@RequestParam String computerId, @RequestParam String action, @RequestParam(required = false) String payload) {
        User user = getCurrentUser();
        Optional<Computer> comp = computerRepository.findById(computerId);

        if (comp.isEmpty() || !user.getId().equals(comp.get().getUserId())) {
            return ResponseEntity.status(403).body("У вас нет прав на управление этим ПК");
        }

        Command cmd = new Command();
        cmd.setComputerId(computerId);
        cmd.setAction(action);
        cmd.setPayload(payload);
        cmd.setStatus("PENDING");
        commandRepository.save(cmd);

        return ResponseEntity.ok("Команда добавлена в очередь");
    }

    @GetMapping("/processes")
    public ResponseEntity<?> getLatestProcesses(@RequestParam String computerId) {
        User user = getCurrentUser();
        return computerRepository.findById(computerId)
                .filter(comp -> user.getId().equals(comp.getUserId()))
                .map(comp -> ResponseEntity.ok(comp.getProcessesSnapshot()))
                .orElse(ResponseEntity.status(403).build());
    }

    @GetMapping("/computers")
    public ResponseEntity<?> getMyComputers() {
        User user = getCurrentUser();
        return ResponseEntity.ok(computerRepository.findByUserId(user.getId()));
    }

    @PostMapping("/computers/link")
    public ResponseEntity<?> linkComputer(@RequestParam String connectionCode) {
        User user = getCurrentUser();

        Optional<Computer> compOpt = computerRepository.findById(connectionCode.toUpperCase());

        if (compOpt.isEmpty()) {
            return ResponseEntity.badRequest().body("ПК с таким кодом не найден. Убедитесь, что программа-агент запущена на компьютере.");
        }

        Computer comp = compOpt.get();

        if (comp.getUserId() != null) {
            if (comp.getUserId().equals(user.getId())) {
                return ResponseEntity.ok("Этот ПК уже привязан к вашему аккаунту");
            }
            return ResponseEntity.badRequest().body("Этот ПК уже привязан к другому пользователю");
        }

        comp.setUserId(user.getId());
        computerRepository.save(comp);

        return ResponseEntity.ok("Компьютер успешно добавлен!");
    }


    @DeleteMapping("/computers/{id}")
    public ResponseEntity<?> unlinkComputer(@PathVariable String id) {
        User user = getCurrentUser(); // Берем текущего юзера из токена
        Optional<Computer> compOpt = computerRepository.findById(id);

        if (compOpt.isEmpty()) {
            return ResponseEntity.notFound().build();
        }

        Computer comp = compOpt.get();

        // Проверяем, что этот ПК принадлежит именно этому пользователю
        if (!user.getId().equals(comp.getUserId())) {
            return ResponseEntity.status(403).body("У вас нет прав на удаление этого узла");
        }

        // Вместо полного удаления из БД мы просто отвязываем его
        comp.setUserId(null);
        computerRepository.save(comp);

        return ResponseEntity.ok("Узел успешно отвязан от вашей учетной записи");
    }


    @GetMapping("/thresholds")
    public ResponseEntity<?> getThresholds(@RequestParam String computerId) {
        NotificationSettings settings = settingsRepository.findByComputerId(computerId);
        if (settings == null) {
            // Возвращаем дефолтные значения, если настроек еще нет
            settings = new NotificationSettings();
            settings.setComputerId(computerId);
            settings.setCpuTempThreshold(80.0);
            settings.setGpuTempThreshold(80.0);
            settings.setDiskTempThreshold(55.0);
        }
        return ResponseEntity.ok(settings);
    }

    @PostMapping("/thresholds")
    public ResponseEntity<?> saveThresholds(@RequestParam String computerId, @RequestBody NotificationSettings newSettings) {
        User user = getCurrentUser(); // Ваш метод получения текущего юзера

        NotificationSettings existing = settingsRepository.findByComputerId(computerId);
        if (existing == null) {
            existing = new NotificationSettings();
            existing.setComputerId(computerId);
            existing.setUserId(user.getId());
        }

        // Обновляем значения
        existing.setCpuTempEnabled(newSettings.isCpuTempEnabled());
        existing.setCpuTempThreshold(newSettings.getCpuTempThreshold());

        existing.setGpuTempEnabled(newSettings.isGpuTempEnabled());
        existing.setGpuTempThreshold(newSettings.getGpuTempThreshold());

        existing.setDiskTempEnabled(newSettings.isDiskTempEnabled());
        existing.setDiskTempThreshold(newSettings.getDiskTempThreshold());

        settingsRepository.save(existing);
        return ResponseEntity.ok("Настройки сохранены");
    }
}