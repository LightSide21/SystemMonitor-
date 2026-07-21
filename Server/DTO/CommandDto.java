package com.monitor.DTO;


import lombok.AllArgsConstructor;
import lombok.Data;

@Data
@AllArgsConstructor
public class CommandDto {
    private Long id;
    private String action;
    private String payload;
}
