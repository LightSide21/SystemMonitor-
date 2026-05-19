package com.monitor.Services;

import com.monitor.Repositories.ComputerRepository;
import lombok.RequiredArgsConstructor;
import org.springframework.scheduling.annotation.Scheduled;
import org.springframework.stereotype.Service;

import java.time.LocalDateTime;

@Service
@RequiredArgsConstructor
public class StatusCheckerService {

    private final ComputerRepository computerRepository;

    // Запускать каждые 15 секунд
    @Scheduled(fixedRate = 15000)
    public void checkOfflineComputers() {
        // Если ПК молчит дольше 20 секунд, считаем его выключенным
        LocalDateTime threshold = LocalDateTime.now().minusSeconds(20);

        computerRepository.findAll().forEach(pc -> {
            if (pc.isOnline() && pc.getLastSeen() != null && pc.getLastSeen().isBefore(threshold)) {
                pc.setOnline(false);
                computerRepository.save(pc);
                System.out.println("ПК отключился: " + pc.getId());
            }
        });
    }
}