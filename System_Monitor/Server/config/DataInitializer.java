package com.monitor.config;

import com.monitor.Entities.Role;
import com.monitor.Entities.User;
import com.monitor.Repositories.RoleRepository;
import com.monitor.Repositories.UserRepository;
import lombok.RequiredArgsConstructor;
import org.springframework.boot.CommandLineRunner;
import org.springframework.security.crypto.password.PasswordEncoder;
import org.springframework.stereotype.Component;

import java.util.Set;

@Component
@RequiredArgsConstructor
public class DataInitializer implements CommandLineRunner {

    private final UserRepository userRepository;
    private final RoleRepository roleRepository;
    private final PasswordEncoder passwordEncoder;

    @Override
    public void run(String... args) {
        // 1. Создаем роли, если их нет
        Role adminRole = roleRepository.findByName("ROLE_ADMIN").orElseGet(() -> {
            Role r = new Role();
            r.setName("ROLE_ADMIN");
            return roleRepository.save(r);
        });

        Role userRole = roleRepository.findByName("ROLE_USER").orElseGet(() -> {
            Role r = new Role();
            r.setName("ROLE_USER");
            return roleRepository.save(r);
        });

        // 2. Создаем админа, если его нет
        if (userRepository.findByUsername("admin").isEmpty()) {
            User admin = new User();
            admin.setUsername("admin");
            admin.setPassword(passwordEncoder.encode("admin")); // Хешируем пароль "admin"
            admin.setRoles(Set.of(adminRole));
            userRepository.save(admin);
            System.out.println("Базовый администратор (admin/admin) успешно создан!");
        }
    }
}