package com.monitor.Services;

import com.influxdb.client.InfluxDBClient;
import com.influxdb.client.WriteApiBlocking;
import com.influxdb.client.domain.WritePrecision;
import com.influxdb.client.write.Point;
import com.influxdb.query.FluxRecord;
import com.influxdb.query.FluxTable;
import com.monitor.DTO.SystemDataDto;
import lombok.RequiredArgsConstructor;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Service;

import java.time.Instant;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

@Service
@RequiredArgsConstructor
public class InfluxService {

    private final InfluxDBClient influxDBClient;

    @Value("${influxdb.bucket}")
    private String bucket;

    @Value("${influxdb.org}")
    private String org;

    public void writeMetrics(SystemDataDto data) {
        WriteApiBlocking writeApi = influxDBClient.getWriteApiBlocking();
        Instant now = Instant.now();

        if (data.getCpu() != null) {
            Point cpu = Point.measurement("cpu")
                    .addTag("computer_id", data.getComputerId())
                    .addField("load", data.getCpu().getLoad())
                    .addField("temp", parseDouble(data.getCpu().getTemperature()))
                    .addField("freq", data.getCpu().getCurrentFrequency())
                    .addField("power", parseDouble(data.getCpu().getPower()))
                    .time(now, WritePrecision.S);
            writeApi.writePoint(bucket, org, cpu);
        }

        if (data.getRam() != null) {
            Point ram = Point.measurement("ram")
                    .addTag("computer_id", data.getComputerId())
                    .addField("load", parseDouble(data.getRam().getLoad()))
                    .addField("used_gb", parseDouble(data.getRam().getUsed()))
                    .time(now, WritePrecision.S);
            writeApi.writePoint(bucket, org, ram);
        }

        if (data.getGpus() != null) {
            for (var gpu : data.getGpus()) {
                Point gpuPoint = Point.measurement("gpu")
                        .addTag("computer_id", data.getComputerId())
                        .addTag("name", gpu.getName())
                        .addField("load", parseDouble(gpu.getUtilization()))
                        .addField("temp", parseDouble(gpu.getTemperature()))
                        .addField("vmemory", parseDouble(gpu.getMemory())) // <-- СОХРАНЯЕМ ПАМЯТЬ
                        .addField("power", parseDouble(gpu.getPower()))
                        .time(now, WritePrecision.S);
                writeApi.writePoint(bucket, org, gpuPoint);
            }
        }

        if (data.getDisks() != null) {
            for (var disk : data.getDisks()) {
                Point diskPoint = Point.measurement("disk")
                        .addTag("computer_id", data.getComputerId())
                        .addTag("model", disk.getModel())
                        .addField("temp", parseDouble(disk.getTemperature()))
                        .addField("total_capacity", parseDouble(disk.getTotalCapacity()))
                        .time(now, WritePrecision.S);
                writeApi.writePoint(bucket, org, diskPoint);

                if (disk.getPartitions() != null) {
                    for (var partition : disk.getPartitions()) {
                        Point partitionPoint = Point.measurement("partition")
                                .addTag("computer_id", data.getComputerId())
                                .addTag("model", disk.getModel())
                                .addTag("drive_letter", partition.getDriveLetter())
                                .addField("total_space", parseDouble(partition.getTotalSpace()))
                                .addField("free_space", parseDouble(partition.getFreeSpace()))
                                .time(now, WritePrecision.S);
                        writeApi.writePoint(bucket, org, partitionPoint);
                    }
                }
            }
        }

        if (data.getNetworks() != null) {
            for(var net : data.getNetworks()) {
                Point netPoint = Point.measurement("network")
                        .addTag("computer_id", data.getComputerId())
                        .addTag("adapter", net.getAdapterName())
                        .addField("up_speed", parseDouble(net.getSendSpeed()))
                        .addField("down_speed", parseDouble(net.getReceiveSpeed()))
                        .time(now, WritePrecision.S);
                writeApi.writePoint(bucket, org, netPoint);
            }
        }
    }

    public Map<String, Object> getComputerMetrics(String computerId, String timeRange) {
        Map<String, Object> result = new HashMap<>();

        String aggWindow = "30m";
        switch (timeRange) {
            case "1h": aggWindow = "1m"; break;
            case "12h": aggWindow = "10m"; break;
            case "24h": aggWindow = "30m"; break;
            case "7d": aggWindow = "3h"; break;
            case "30d": aggWindow = "12h"; break;
            default: timeRange = "24h"; aggWindow = "30m"; break;
        }

        try {
            String currentQuery = String.format("from(bucket: \"%s\") |> range(start: -2m) |> filter(fn: (r) => r.computer_id == \"%s\") |> last()", bucket, computerId);
            List<FluxTable> tables = influxDBClient.getQueryApi().query(currentQuery, org);


            double cpuLoad = 0, cpuTemp = 0, ramLoad = 0, gpuLoad = 0, gpuTemp = 0, gpuMem = 0;
            double cpuFreq = 0, cpuPower = 0, gpuPower = 0;

            Map<String, Map<String, Object>> networksMap = new HashMap<>();
            Map<String, Map<String, Object>> physicalDisksMap = new HashMap<>();
            Map<String, Map<String, Object>> partitionsMap = new HashMap<>();

            for (FluxTable table : tables) {
                for (FluxRecord record : table.getRecords()) {
                    String measurement = record.getMeasurement();
                    String field = record.getField();
                    double value = (record.getValue() instanceof Number) ? ((Number) record.getValue()).doubleValue() : 0.0;

                    if ("cpu".equals(measurement)) {
                        if ("load".equals(field)) cpuLoad = value;
                        if ("temp".equals(field)) cpuTemp = value;
                        if ("freq".equals(field)) cpuFreq = value;
                        if ("power".equals(field)) cpuPower = value;
                    } else if ("ram".equals(measurement)) {
                        if ("load".equals(field)) ramLoad = value;
                    } else if ("gpu".equals(measurement)) {
                        if ("load".equals(field)) gpuLoad = value;
                        if ("temp".equals(field)) gpuTemp = value;
                        if ("power".equals(field)) gpuPower = value;
                        if ("vmemory".equals(field)) gpuMem = value; //
                    } else if ("network".equals(measurement)) {
                        String adapter = (String) record.getValueByKey("adapter");
                        if (adapter != null) {
                            networksMap.putIfAbsent(adapter, new HashMap<>());
                            networksMap.get(adapter).put("name", adapter);
                            if ("up_speed".equals(field)) networksMap.get(adapter).put("up", value);
                            if ("down_speed".equals(field)) networksMap.get(adapter).put("down", value);
                        }
                    } else if ("disk".equals(measurement)) {
                        String model = (String) record.getValueByKey("model");
                        if (model != null) {
                            physicalDisksMap.putIfAbsent(model, new HashMap<>());
                            physicalDisksMap.get(model).put("model", model);
                            physicalDisksMap.get(model).put(field, value);
                        }
                    } else if ("partition".equals(measurement)) {
                        String drive = (String) record.getValueByKey("drive_letter");
                        if (drive != null) {
                            partitionsMap.putIfAbsent(drive, new HashMap<>());
                            partitionsMap.get(drive).put("driveLetter", drive);
                            partitionsMap.get(drive).put(field, value);
                        }
                    }
                }
            }

            for (Map<String, Object> p : partitionsMap.values()) {
                double total = (double) p.getOrDefault("total_space", 0.0);
                double free = (double) p.getOrDefault("free_space", 0.0);
                double used = total - free;
                double usedPct = total > 0 ? (used / total) * 100 : 0.0;
                p.put("used_space", used);
                p.put("used_percent", usedPct);
            }

            result.put("cpuLoad", cpuLoad); result.put("cpuTemp", cpuTemp); result.put("ramLoad", ramLoad);
            result.put("gpuLoad", gpuLoad); result.put("gpuTemp", gpuTemp); result.put("gpuMem", gpuMem); // <-- КЛАДЕМ В ОТВЕТ
            result.put("cpuFreq", cpuFreq); result.put("cpuPower", cpuPower); result.put("gpuPower", gpuPower);
            result.put("networks", networksMap.values());
            result.put("physicalDisks", physicalDisksMap.values());
            result.put("partitions", partitionsMap.values());

            String cpuHistoryQuery = String.format("from(bucket: \"%s\") |> range(start: -%s) |> filter(fn: (r) => r.computer_id == \"%s\" and r._measurement == \"cpu\" and (r._field == \"load\" or r._field == \"temp\")) |> aggregateWindow(every: %s, fn: mean, createEmpty: false)", bucket, timeRange, computerId, aggWindow);
            List<Double> cpuLoadHist = new ArrayList<>(), cpuTempHist = new ArrayList<>();
            for (FluxTable table : influxDBClient.getQueryApi().query(cpuHistoryQuery, org)) {
                for (FluxRecord record : table.getRecords()) {
                    double val = (record.getValue() instanceof Number) ? ((Number) record.getValue()).doubleValue() : 0.0;
                    if ("load".equals(record.getField())) cpuLoadHist.add(val);
                    if ("temp".equals(record.getField())) cpuTempHist.add(val);
                }
            }
            result.put("cpuLoadHistory", cpuLoadHist); result.put("cpuTempHistory", cpuTempHist);

            String ramHistoryQuery = String.format("from(bucket: \"%s\") |> range(start: -%s) |> filter(fn: (r) => r.computer_id == \"%s\" and r._measurement == \"ram\" and r._field == \"load\") |> aggregateWindow(every: %s, fn: mean, createEmpty: false)", bucket, timeRange, computerId, aggWindow);
            List<Double> ramHist = new ArrayList<>();
            for (FluxTable table : influxDBClient.getQueryApi().query(ramHistoryQuery, org)) {
                for (FluxRecord record : table.getRecords()) {
                    ramHist.add((record.getValue() instanceof Number) ? ((Number) record.getValue()).doubleValue() : 0.0);
                }
            }
            result.put("ramLoadHistory", ramHist);

            String gpuHistoryQuery = String.format("from(bucket: \"%s\") |> range(start: -%s) |> filter(fn: (r) => r.computer_id == \"%s\" and r._measurement == \"gpu\" and (r._field == \"load\" or r._field == \"temp\")) |> aggregateWindow(every: %s, fn: mean, createEmpty: false)", bucket, timeRange, computerId, aggWindow);
            List<Double> gpuLoadHist = new ArrayList<>(), gpuTempHist = new ArrayList<>();
            for (FluxTable table : influxDBClient.getQueryApi().query(gpuHistoryQuery, org)) {
                for (FluxRecord record : table.getRecords()) {
                    double val = (record.getValue() instanceof Number) ? ((Number) record.getValue()).doubleValue() : 0.0;
                    if ("load".equals(record.getField())) gpuLoadHist.add(val);
                    if ("temp".equals(record.getField())) gpuTempHist.add(val);
                }
            }
            result.put("gpuLoadHistory", gpuLoadHist); result.put("gpuTempHistory", gpuTempHist);

            String netHistoryQuery = String.format("from(bucket: \"%s\") |> range(start: -%s) |> filter(fn: (r) => r.computer_id == \"%s\" and r._measurement == \"network\" and (r._field == \"up_speed\" or r._field == \"down_speed\")) |> aggregateWindow(every: %s, fn: max, createEmpty: false)", bucket, timeRange, computerId, aggWindow);
            for (FluxTable table : influxDBClient.getQueryApi().query(netHistoryQuery, org)) {
                if (table.getRecords().isEmpty()) continue;
                for (FluxRecord record : table.getRecords()) {
                    String adapter = (String) record.getValueByKey("adapter");
                    if (adapter == null) adapter = "Unknown";

                    networksMap.putIfAbsent(adapter, new HashMap<>());
                    networksMap.get(adapter).put("name", adapter);

                    String field = record.getField();
                    String targetKey = "up_speed".equals(field) ? "upHistory" : "downHistory";

                    List<Double> hist = (List<Double>) networksMap.get(adapter).getOrDefault(targetKey, new ArrayList<Double>());
                    hist.add((record.getValue() instanceof Number) ? ((Number) record.getValue()).doubleValue() : 0.0);
                    networksMap.get(adapter).put(targetKey, hist);
                }
            }
            result.put("networks", networksMap.values());

        } catch (Exception e) {
            System.err.println("Ошибка при чтении из InfluxDB: " + e.getMessage());
            e.printStackTrace();
        }
        return result;
    }

    private double parseDouble(String value) {
        if (value == null || value.isEmpty()) return 0.0;
        try {
            Matcher matcher = Pattern.compile("[\\d.,]+").matcher(value);
            if (matcher.find()) {
                return Double.parseDouble(matcher.group().replace(",", "."));
            }
        } catch (Exception ignored) {}
        return 0.0;
    }
}