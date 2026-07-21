package com.monitor.Repositories;
import com.monitor.Entities.Command;
import org.springframework.data.jpa.repository.JpaRepository;
import java.util.Optional;

public interface CommandRepository extends JpaRepository<Command, Long> {
    // Ищем самую старую невыполненную команду для конкретного ПК
    Optional<Command> findFirstByComputerIdAndStatusOrderByCreatedAtAsc(String computerId, String status);
}
