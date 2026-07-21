package com.monitor.Services;

import com.monitor.DTO.SystemDataDto;
import com.monitor.Entities.Computer;
import com.monitor.Repositories.ComputerRepository;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;

import java.time.LocalDateTime;
import java.util.stream.Collectors;

@Service
@RequiredArgsConstructor
public class AgentService {

    private final ComputerRepository computerRepository;



    public void processIncomingData(SystemDataDto data) {
        String incomingHash = data.getConfigHash();
        String computerId = data.getComputerId();

        //  Ищем компьютер в БД
        Computer computer = computerRepository.findById(computerId)
                .orElse(new Computer());

        if (computer.getId() == null) {
            computer.setId(computerId);
            // При первом подключении ПК пока не привязан к конкретному пользователю
        }

        //Сравниваем хеши
        String currentHash = computer.getConfigHash();
        boolean hardwareChanged = (currentHash == null) || !currentHash.equals(incomingHash);

        boolean needsRetroactiveUpdate = (computer.getOsName() == null || computer.getLogicalCores() == null);

        //  Если железо поменялось (или ПК подключился впервые) — перезаписываем комплектующие
        if (hardwareChanged || needsRetroactiveUpdate) {
            System.out.println("Обновление конфигурации железа для: " + computerId);
            computer.setName(data.getComputerName());
            computer.setOsName(data.getOs().getName() + " (" + data.getOs().getArchitecture() + ")");
            computer.setConfigHash(incomingHash);


            if (data.getOs() != null) {
                computer.setOsName(data.getOs().getName() + " (" + data.getOs().getArchitecture() + ")");
            }

            // Сохраняем ТОЛЬКО названия
            if (data.getCpu() != null) {
                computer.setCpuName(data.getCpu().getName());
                computer.setLogicalCores(data.getCpu().getLogicalCores());
                computer.setPhysicalCores(data.getCpu().getPhysicalCores());
            }

            if (data.getRam() != null) {
                computer.setRamTotal(data.getRam().getTotal());
            }

            if (data.getGpus() != null && !data.getGpus().isEmpty()) {
                computer.setGpuNames(data.getGpus().stream()
                        .map(SystemDataDto.GpuDto::getName)
                        .collect(Collectors.joining(", ")));
            }

            if (data.getDisks() != null && !data.getDisks().isEmpty()) {
                computer.setStorageInfo(data.getDisks().stream()
                        .map(d -> d.getModel() + " (" + d.getTotalCapacity() + ")")
                        .collect(Collectors.joining(" | ")));
            }


        }

        //  Обновляем статус (ПК сейчас в сети)
        computer.setLastSeen(LocalDateTime.now());
        computer.setOnline(true);

        computerRepository.save(computer);
    }
}