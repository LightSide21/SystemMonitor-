import React, { useEffect, useState, useContext, useRef } from 'react';
import { View, Text, StyleSheet, ScrollView, Dimensions, ActivityIndicator, TouchableOpacity, Switch, TextInput, Alert } from 'react-native';
import { LineChart, ProgressChart } from 'react-native-chart-kit';
import { AuthContext } from '../context/AuthContext';
import { BASE_URL } from '../config';
import * as Notifications from 'expo-notifications';

try {
  Notifications.setNotificationHandler({ handleNotification: async () => ({ shouldShowAlert: true, shouldPlaySound: true, shouldSetBadge: false }) });
} catch (e) { console.log(e); }

const screenWidth = Dimensions.get('window').width;

const getSafeHistory = (arr) => (!arr || arr.length === 0) ? [0, 0] : (arr.length === 1 ? [arr[0], arr[0]] : arr);

const syncLengths = (arr1, arr2) => {
  const s1 = getSafeHistory(arr1), s2 = getSafeHistory(arr2);
  const maxLen = Math.max(s1.length, s2.length);
  const pad = (arr, len) => { const res = [...arr]; while(res.length < len) res.push(res[res.length - 1] || 0); return res.slice(0, len); };
  return [pad(s1, maxLen), pad(s2, maxLen)];
};

const generateXLabels = (count, range) => {
  if (count < 2) return new Array(count).fill("");
  const labelCount = 5, labels = new Array(count).fill(""), now = new Date();
  for (let i = 0; i < labelCount; i++) {
    const dataIdx = Math.floor((i * (count - 1)) / (labelCount - 1));
    const labelDate = new Date(now);
    if (range === '1h') { labelDate.setMinutes(now.getMinutes() - (60 - Math.floor((i * 60) / (labelCount - 1)))); labels[dataIdx] = `${labelDate.getHours()}:${labelDate.getMinutes().toString().padStart(2, '0')}`; }
    else if (range === '12h' || range === '24h') { labelDate.setHours(now.getHours() - ((range === '12h' ? 12 : 24) - Math.floor((i * (range === '12h' ? 12 : 24)) / (labelCount - 1)))); labels[dataIdx] = `${labelDate.getHours()}:00`; }
    else if (range === '7d' || range === '30d') { labelDate.setDate(now.getDate() - ((range === '7d' ? 7 : 30) - Math.floor((i * (range === '7d' ? 7 : 30)) / (labelCount - 1)))); labels[dataIdx] = `${labelDate.getDate()}.${labelDate.getMonth() + 1}`; }
  }
  labels[count - 1] = "Сейчас"; return labels;
};

const TIME_RANGES = [{ label: '1Ч', value: '1h' }, { label: '12Ч', value: '12h' }, { label: '24Ч', value: '24h' }, { label: '7Д', value: '7d' }, { label: '30Д', value: '30d' }];

const formatGpuMem = (val) => {
  if (!val) return '--';
  if (val < 100) return val.toFixed(1) + ' GB';
  return val >= 1000 ? (val / 1024).toFixed(1) + ' GB' : Math.round(val) + ' MB';
};

const InfoRow = ({ label, value, theme }) => (
  <View style={[styles.infoRow, { borderBottomColor: theme.border }]}>
    <Text style={{ color: theme.textMuted, fontSize: 13, flex: 1 }}>{label}</Text>
    <Text style={{ color: theme.text, fontSize: 13, fontWeight: 'bold', flex: 2, textAlign: 'right' }}>{value}</Text>
  </View>
);

const Accordion = ({ title, subTitle, children, defaultExpanded = false, theme }) => {
  const [expanded, setExpanded] = useState(defaultExpanded);
  return (
    <View style={[styles.accordionContainer, { backgroundColor: theme.cardBg, borderColor: theme.border }]}>
      <TouchableOpacity style={styles.accordionHeader} onPress={() => setExpanded(!expanded)} activeOpacity={0.7}>
        <View style={{ flex: 1 }}>
          <Text style={[styles.accordionTitle, { color: theme.text }]}>{title}</Text>
          <Text style={[styles.accordionSubTitle, { color: theme.textMuted }]} numberOfLines={1}>{subTitle}</Text>
        </View>
        <Text style={{ fontSize: 18, color: theme.primary }}>{expanded ? '▲' : '▼'}</Text>
      </TouchableOpacity>
      {expanded && <View style={[styles.accordionBody, { borderTopColor: theme.border }]}>{children}</View>}
    </View>
  );
};

export default function ComputerDetailsScreen({ route }) {
  if (!route || !route.params || !route.params.computer) return null;
  const { computer } = route.params;
  const { userToken, theme } = useContext(AuthContext);

  const [timeRange, setTimeRange] = useState('1h');
  const [metrics, setMetrics] = useState({ cpuLoad: 0, cpuTemp: 0, ramLoad: 0, gpuLoad: 0, gpuTemp: 0, gpuMem: 0, cpuFreq: 0, cpuPower: 0, gpuPower: 0, cpuLoadHistory: [], cpuTempHistory: [], ramLoadHistory: [], gpuLoadHistory: [], gpuTempHistory: [], networks: [], physicalDisks: [], partitions: [] });
  const [loading, setLoading] = useState(true);
  const [chartLoading, setChartLoading] = useState(false);
  const [thresholds, setThresholds] = useState({ cpuTempEnabled: false, cpuTempThreshold: 80, gpuTempEnabled: false, gpuTempThreshold: 80, diskTempEnabled: false, diskTempThreshold: 55 });

  const thresholdsRef = useRef(thresholds);
  useEffect(() => { thresholdsRef.current = thresholds; }, [thresholds]);
  const lastAlertTime = useRef({ cpu: 0, gpu: 0, disk: 0 });

  useEffect(() => { try { Notifications.requestPermissionsAsync(); } catch(e) {} }, []);

  const fetchMetrics = async (isBackgroundUpdate = false) => {
    if (!isBackgroundUpdate) setChartLoading(true);
    try {
      const response = await fetch(`${BASE_URL}/metrics/dashboard?computerId=${computer.id}&timeRange=${timeRange}`, { headers: { 'Authorization': `Bearer ${userToken}` } });
      if (response.ok) {
        const data = await response.json(); setMetrics(prev => ({ ...prev, ...data }));
        const now = Date.now(), currentThresh = thresholdsRef.current;
        try {
          if (currentThresh.cpuTempEnabled && data.cpuTemp >= currentThresh.cpuTempThreshold) { if (now - lastAlertTime.current.cpu > 300000) { Notifications.scheduleNotificationAsync({ content: { title: "Перегрев CPU!", body: `Температура: ${Math.round(data.cpuTemp)}°C`, sound: true }, trigger: null }); lastAlertTime.current.cpu = now; } }
          if (currentThresh.gpuTempEnabled && data.gpuTemp >= currentThresh.gpuTempThreshold) { if (now - lastAlertTime.current.gpu > 300000) { Notifications.scheduleNotificationAsync({ content: { title: "Перегрев GPU!", body: `Температура: ${Math.round(data.gpuTemp)}°C`, sound: true }, trigger: null }); lastAlertTime.current.gpu = now; } }
          if (currentThresh.diskTempEnabled && data.physicalDisks?.length > 0) { const hotDisk = data.physicalDisks.find(d => d.temp >= currentThresh.diskTempThreshold); if (hotDisk) { if (now - lastAlertTime.current.disk > 300000) { Notifications.scheduleNotificationAsync({ content: { title: "Перегрев Диска!", body: `Диск нагрелся до ${Math.round(hotDisk.temp)}°C`, sound: true }, trigger: null }); lastAlertTime.current.disk = now; } } }
        } catch(e) {}
      }
    } catch (e) {} finally { setLoading(false); setChartLoading(false); }
  };

  const fetchSettings = async () => {
    try { const response = await fetch(`${BASE_URL}/manage/thresholds?computerId=${computer.id}`, { headers: { 'Authorization': `Bearer ${userToken}` } }); if (response.ok) setThresholds(await response.json()); } catch (e) {}
  };

  const saveThresholds = async (newSettings) => {
    setThresholds(newSettings);
    try { await fetch(`${BASE_URL}/manage/thresholds?computerId=${computer.id}`, { method: 'POST', headers: { 'Authorization': `Bearer ${userToken}`, 'Content-Type': 'application/json' }, body: JSON.stringify(newSettings) }); } catch (e) { Alert.alert("Ошибка"); }
  };

  useEffect(() => { fetchMetrics(false); fetchSettings(); const interval = setInterval(() => fetchMetrics(true), 5000); return () => clearInterval(interval); }, [computer.id, timeRange]);

  if (loading) return <View style={[styles.loader, { backgroundColor: theme.bg }]}><ActivityIndicator size="large" color={theme.primary} /></View>;

  const solidCardBg = theme.isDark ? '#151921' : '#EBEDF0';
  const solidMainBg = theme.bg;

  const ringConfig = (color) => ({
    backgroundColor: solidCardBg,
    backgroundGradientFrom: solidCardBg,
    backgroundGradientTo: solidCardBg,
    color: () => color
  });

  const lineConfig = (lineColor, fillColor) => ({
    backgroundColor: solidMainBg,
    backgroundGradientFrom: solidMainBg,
    backgroundGradientTo: solidMainBg,
    color: () => lineColor,
    labelColor: () => theme.textMuted,
    strokeWidth: 2,
    fillShadowGradient: fillColor,
    fillShadowGradientOpacity: 0.3,
    propsForDots: { r: "0" },
    propsForBackgroundLines: { stroke: theme.border, strokeDasharray: '3' },
    propsForLabels: { fontSize: 10 }
  });

  const renderThresholdControl = (type, label) => (
    <View style={[styles.thresholdBox, { backgroundColor: theme.glassInput, borderColor: theme.border }]}>
      <View style={styles.thresholdRow}>
        <Text style={[styles.thresholdLabel, { color: theme.primary }]}>Push-уведомление ({label})</Text>
        <Switch value={thresholds[`${type}Enabled`]} onValueChange={(val) => saveThresholds({...thresholds, [`${type}Enabled`]: val})} trackColor={{ false: theme.border, true: theme.primary }} />
      </View>
      {thresholds[`${type}Enabled`] && (
        <View style={styles.inputRow}>
          <Text style={{ color: theme.textMuted, fontSize: 12 }}>Оповестить при ≥ </Text>
          <TextInput style={[styles.thresholdInput, { backgroundColor: theme.bg, color: theme.text, borderColor: theme.primary }]} keyboardType="numeric" value={thresholds[`${type}Threshold`].toString()} onChangeText={(txt) => saveThresholds({...thresholds, [`${type}Threshold`]: parseInt(txt) || 0})} />
          <Text style={{ color: theme.textMuted, fontSize: 12 }}> °C</Text>
        </View>
      )}
    </View>
  );

  return (
    <ScrollView style={[styles.container, { backgroundColor: theme.bg }]} showsVerticalScrollIndicator={false}>
      <Text style={[styles.headerTitle, { color: theme.text }]}>{computer.name || computer.id}</Text>

      <Accordion title="О ПК" subTitle="Системная информация" defaultExpanded={false} theme={theme}>
        <View style={{ marginTop: 5 }}>
          <InfoRow theme={theme} label="Операционная система" value={computer.osName || 'Неизвестно'} />
          <InfoRow theme={theme} label="Процессор (CPU)" value={computer.cpuName || '--'} />
          <InfoRow theme={theme} label="Ядра CPU" value={computer.logicalCores ? `${computer.physicalCores} физ. / ${computer.logicalCores} лог.` : '--'} />
          <InfoRow theme={theme} label="ОЗУ (RAM)" value={computer.ramTotal || '--'} />
          <InfoRow theme={theme} label="Видеокарта (GPU)" value={computer.gpuNames || '--'} />

          <InfoRow
            theme={theme}
            label="Накопители"
            value={metrics.physicalDisks?.length > 0 ? metrics.physicalDisks.map(d => d.model).join(', ') : (computer.storageInfo || '--')}
          />
        </View>
      </Accordion>

      <View style={[styles.summaryRow, { backgroundColor: solidCardBg }]}>
        <View style={styles.ringItem}><ProgressChart data={{ data: [Number(metrics.ramLoad) / 100 || 0] }} width={80} height={80} strokeWidth={6} radius={32} chartConfig={ringConfig('#4A90E2')} hideLegend={true} /><Text style={[styles.ringValue, {color: theme.text}]}>{(metrics.ramLoad || 0).toFixed(1)}%</Text><Text style={[styles.ringLabel, {color: theme.textMuted}]}>RAM</Text></View>
        <View style={styles.ringItem}><ProgressChart data={{ data: [Number(metrics.cpuLoad) / 100 || 0] }} width={80} height={80} strokeWidth={6} radius={32} chartConfig={ringConfig(theme.danger)} hideLegend={true} /><Text style={[styles.ringValue, {color: theme.text}]}>{Math.round(metrics.cpuLoad || 0)}%</Text><Text style={[styles.ringLabel, {color: theme.textMuted}]}>CPU</Text></View>
        <View style={styles.ringItem}><ProgressChart data={{ data: [Number(metrics.gpuLoad) / 100 || 0] }} width={80} height={80} strokeWidth={6} radius={32} chartConfig={ringConfig(theme.primary)} hideLegend={true} /><Text style={[styles.ringValue, {color: theme.text}]}>{Math.round(metrics.gpuLoad || 0)}%</Text><Text style={[styles.ringLabel, {color: theme.textMuted}]}>GPU</Text></View>
      </View>

      <View style={[styles.timeRangeContainer, { backgroundColor: theme.cardBg }]}>
        {TIME_RANGES.map((item) => (
          <TouchableOpacity key={item.value} style={[styles.timeRangeBtn, timeRange === item.value && { backgroundColor: theme.primary }]} onPress={() => setTimeRange(item.value)}>
            <Text style={{ fontSize: 12, fontWeight: '600', color: timeRange === item.value ? '#0B0E14' : theme.textMuted }}>{item.label}</Text>
          </TouchableOpacity>
        ))}
      </View>

      {chartLoading && <ActivityIndicator size="small" color={theme.primary} style={{marginBottom: 10}} />}

      <Accordion title="CPU" subTitle={computer.cpuName || '--'} defaultExpanded={true} theme={theme}>
        <View style={{ marginTop: 10 }}>
          <View style={styles.gridRow}>
            <View style={styles.gridCol}><Text style={{fontSize:12, color: theme.textMuted}}>Загрузка</Text><Text style={{fontSize:18, fontWeight:'600', color:theme.text}}>{Math.round(metrics.cpuLoad)}%</Text></View>
            <View style={styles.gridCol}><Text style={{fontSize:12, color: theme.textMuted}}>Скорость</Text><Text style={{fontSize:18, fontWeight:'600', color:theme.text}}>{metrics.cpuFreq > 0 ? (metrics.cpuFreq / 1000).toFixed(2) + ' GHz' : '--'}</Text></View>
          </View>
          <View style={styles.gridRow}>
            <View style={styles.gridCol}><Text style={{fontSize:12, color: theme.textMuted}}>Температура</Text><Text style={{fontSize:18, fontWeight:'600', color:theme.text}}>{Math.round(metrics.cpuTemp)}°C</Text></View>
            <View style={styles.gridCol}><Text style={{fontSize:12, color: theme.textMuted}}>Питание</Text><Text style={{fontSize:18, fontWeight:'600', color:theme.text}}>{metrics.cpuPower > 0 ? metrics.cpuPower.toFixed(1) + ' W' : '--'}</Text></View>
          </View>
        </View>
        <Text style={{color: theme.textMuted, fontSize: 12, marginLeft: 5}}>Загрузка (%)</Text>

        {}
        <LineChart data={{ labels: generateXLabels(syncLengths(metrics.cpuLoadHistory, [])[0].length, timeRange), datasets: [{ data: syncLengths(metrics.cpuLoadHistory, [])[0] }] }} width={screenWidth - 5} height={170} chartConfig={lineConfig('#4A90E2', '#4A90E2')} bezier fromZero formatYLabel={(y) => String(Math.round(Number(y) || 0))} style={styles.lineChart} />

        <Text style={{color: theme.textMuted, fontSize: 12, marginLeft: 5}}>Температура (°C)</Text>
        <LineChart data={{ labels: generateXLabels(syncLengths(metrics.cpuTempHistory, [])[0].length, timeRange), datasets: [{ data: syncLengths(metrics.cpuTempHistory, [])[0] }] }} width={screenWidth - 5} height={170} chartConfig={lineConfig(theme.danger, theme.danger)} bezier fromZero formatYLabel={(y) => String(Math.round(Number(y) || 0))} style={styles.lineChart} />
        {renderThresholdControl('cpuTemp', 'Перегрев CPU')}
      </Accordion>

      <Accordion title="Memory" subTitle={computer.ramTotal || '--'} theme={theme}>
        <View style={{ marginTop: 10 }}>
          <View style={styles.gridRow}>
            <View style={styles.gridCol}><Text style={{fontSize:12, color: theme.textMuted}}>Используется</Text><Text style={{fontSize:18, fontWeight:'600', color:theme.text}}>{(metrics.ramLoad || 0).toFixed(1)}%</Text></View>
            <View style={styles.gridCol}><Text style={{fontSize:12, color: theme.textMuted}}>Всего памяти</Text><Text style={{fontSize:18, fontWeight:'600', color:theme.text}}>{computer.ramTotal || '--'}</Text></View>
          </View>
        </View>
        <Text style={{color: theme.textMuted, fontSize: 12, marginLeft: 5}}>Заполнение памяти (%)</Text>
        <LineChart data={{ labels: generateXLabels(syncLengths(metrics.ramLoadHistory, [])[0].length, timeRange), datasets: [{ data: syncLengths(metrics.ramLoadHistory, [])[0] }] }} width={screenWidth - 5} height={170} chartConfig={lineConfig('#8A2BE2', '#8A2BE2')} bezier fromZero formatYLabel={(y) => String(Math.round(Number(y) || 0))} style={styles.lineChart} />
      </Accordion>

      <Accordion title="GPU" subTitle={computer.gpuNames || '--'} theme={theme}>
        <View style={{ marginTop: 10 }}>
          <View style={styles.gridRow}>
            <View style={styles.gridCol}><Text style={{fontSize:12, color: theme.textMuted}}>Использование</Text><Text style={{fontSize:18, fontWeight:'600', color:theme.text}}>{Math.round(metrics.gpuLoad)}%</Text></View>
            <View style={styles.gridCol}><Text style={{fontSize:12, color: theme.textMuted}}>Температура</Text><Text style={{fontSize:18, fontWeight:'600', color:theme.text}}>{Math.round(metrics.gpuTemp)}°C</Text></View>
          </View>
          <View style={styles.gridRow}>
             <View style={styles.gridCol}><Text style={{fontSize:12, color: theme.textMuted}}>Питание</Text><Text style={{fontSize:18, fontWeight:'600', color:theme.text}}>{metrics.gpuPower > 0 ? metrics.gpuPower.toFixed(1) + ' W' : '--'}</Text></View>
             <View style={styles.gridCol}><Text style={{fontSize:12, color: theme.textMuted}}>Видеопамять</Text><Text style={{fontSize:18, fontWeight:'600', color:theme.text}}>{formatGpuMem(metrics.gpuMem)}</Text></View>
          </View>
        </View>
        <Text style={{color: theme.textMuted, fontSize: 12, marginLeft: 5}}>Загрузка (%)</Text>
        <LineChart data={{ labels: generateXLabels(syncLengths(metrics.gpuLoadHistory, [])[0].length, timeRange), datasets: [{ data: syncLengths(metrics.gpuLoadHistory, [])[0] }] }} width={screenWidth - 5} height={170} chartConfig={lineConfig(theme.primary, theme.primary)} bezier fromZero formatYLabel={(y) => String(Math.round(Number(y) || 0))} style={styles.lineChart} />

        <Text style={{color: theme.textMuted, fontSize: 12, marginLeft: 5}}>Температура (°C)</Text>
        <LineChart data={{ labels: generateXLabels(syncLengths(metrics.gpuTempHistory, [])[0].length, timeRange), datasets: [{ data: syncLengths(metrics.gpuTempHistory, [])[0] }] }} width={screenWidth - 5} height={170} chartConfig={lineConfig(theme.danger, theme.danger)} bezier fromZero formatYLabel={(y) => String(Math.round(Number(y) || 0))} style={styles.lineChart} />
        {renderThresholdControl('gpuTemp', 'Перегрев GPU')}
      </Accordion>

      <Accordion title="Disks" subTitle="Накопители" theme={theme}>
        {metrics.physicalDisks?.map((disk, i) => (
          <View key={`phys-${i}`} style={[styles.physicalDiskBox, { backgroundColor: theme.cardBg, borderColor: theme.border }]}>
            <Text style={{ color: theme.text, fontWeight: 'bold' }}>{disk.model}</Text>
            <Text style={{ color: theme.textMuted, fontSize: 12, marginTop: 5 }}>🌡 {Math.round(disk.temp || 0)}°C | 📦 {disk.total_capacity?.toFixed(0)} GB</Text>
          </View>
        ))}

        {metrics.partitions && metrics.partitions.length > 0 && (
          <View style={{ marginTop: 10 }}>
            <Text style={{ color: theme.textMuted, fontSize: 11, fontWeight: 'bold', marginBottom: 10 }}>ЛОКАЛЬНЫЕ ДИСКИ:</Text>
            {metrics.partitions.map((part, j) => (
              <View key={`part-${j}`} style={[styles.partitionBox, { backgroundColor: theme.cardBg, borderColor: theme.border }]}>
                <View style={styles.partitionTextRow}>
                  <Text style={{ color: theme.text, fontSize: 14, fontWeight: 'bold' }}>Диск {part.driveLetter}</Text>
                  <Text style={{ color: theme.textMuted, fontSize: 11 }}>
                    {part.used_space?.toFixed(1)} GB / {part.free_space?.toFixed(1)} GB своб.
                  </Text>
                </View>
                <View style={styles.progressContainer}>
                  <View
                    style={[
                      styles.progressLine,
                      {
                        width: `${Math.min(part.used_percent || 0, 100)}%`,
                        backgroundColor: (part.used_percent || 0) > 90 ? theme.danger : '#4A90E2'
                      }
                    ]}
                  />
                </View>
              </View>
            ))}
          </View>
        )}
        {renderThresholdControl('diskTemp', 'Перегрев Дисков')}
      </Accordion>

      <Accordion title="Network" subTitle="Адаптеры" theme={theme}>
        {metrics.networks?.map((net, i) => {
          const [sDown, sUp] = syncLengths(net.downHistory, net.upHistory);
          return (
            <View key={i} style={{ marginBottom: 30 }}>
              <Text style={{ color: theme.text, fontWeight: 'bold', marginBottom: 15 }}>{net.name}</Text>

              <View style={styles.gridRow}>
                <View style={styles.gridCol}>
                  <Text style={{fontSize:12, color: theme.danger, fontWeight: 'bold'}}>Отправка ↗</Text>
                  <Text style={{fontSize:18, fontWeight:'600', color:theme.text}}>{Math.round(net.up || 0)} Kbps</Text>
                </View>
                <View style={styles.gridCol}>
                  <Text style={{fontSize:12, color: '#4A90E2', fontWeight: 'bold'}}>Получение ↙</Text>
                  <Text style={{fontSize:18, fontWeight:'600', color:theme.text}}>{Math.round(net.down || 0)} Kbps</Text>
                </View>
              </View>

              <LineChart
                data={{
                  labels: generateXLabels(sDown.length, timeRange),
                  datasets: [{ data: sDown, color: () => '#4A90E2' }, { data: sUp, color: () => theme.danger }]
                }}
                width={screenWidth - 5}
                height={170}
                chartConfig={lineConfig('#4A90E2', solidMainBg)}
                bezier
                fromZero
                formatYLabel={(y) => `${Math.round(Number(y) || 0)}`}
                style={styles.lineChart}
              />
            </View>
          );
        })}
      </Accordion>
      <View style={{ height: 50 }} />
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  loader: { flex: 1, justifyContent: 'center' },
  container: { flex: 1, padding: 15 },
  headerTitle: { fontSize: 22, fontWeight: 'bold', marginBottom: 20, textAlign: 'center' },
  summaryRow: { flexDirection: 'row', justifyContent: 'space-around', padding: 15, borderRadius: 12, marginBottom: 20 },
  ringItem: { alignItems: 'center', justifyContent: 'center' },
  ringValue: { position: 'absolute', fontSize: 16, fontWeight: 'bold', top: 30 },
  ringLabel: { fontSize: 12, marginTop: 5 },
  timeRangeContainer: { flexDirection: 'row', justifyContent: 'space-between', borderRadius: 8, padding: 4, marginBottom: 15 },
  timeRangeBtn: { flex: 1, paddingVertical: 8, alignItems: 'center', borderRadius: 6 },
  accordionContainer: { borderRadius: 10, marginBottom: 15, overflow: 'hidden', borderWidth: 1 },
  accordionHeader: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', padding: 15 },
  accordionTitle: { fontSize: 16, fontWeight: 'bold' },
  accordionSubTitle: { fontSize: 12, marginTop: 4, paddingRight: 20 },
  accordionBody: { padding: 15, paddingTop: 5, borderTopWidth: 1 },


  lineChart: { borderRadius: 8, marginBottom: 25, marginTop: 15, marginLeft: -28 },

  physicalDiskBox: { borderRadius: 8, padding: 12, marginBottom: 10, borderWidth: 1 },
  partitionBox: { marginBottom: 15, padding: 12, borderRadius: 8, borderWidth: 1 },
  partitionTextRow: { flexDirection: 'row', justifyContent: 'space-between', marginBottom: 8 },
  progressContainer: { height: 8, backgroundColor: '#161A23', borderRadius: 4, overflow: 'hidden' },
  progressLine: { height: '100%', borderRadius: 4 },
  gridRow: { flexDirection: 'row', justifyContent: 'space-between', marginBottom: 10 },
  gridCol: { flex: 1 },
  thresholdBox: { marginTop: 15, marginBottom: 15, padding: 12, borderRadius: 10, borderWidth: 1 },
  thresholdRow: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  thresholdLabel: { fontSize: 13, fontWeight: 'bold', letterSpacing: 0.5 },
  inputRow: { flexDirection: 'row', alignItems: 'center', marginTop: 10 },
  thresholdInput: { paddingHorizontal: 10, paddingVertical: 4, borderRadius: 6, minWidth: 45, textAlign: 'center', fontWeight: 'bold', borderWidth: 1 },
  infoRow: { flexDirection: 'row', justifyContent: 'space-between', paddingVertical: 10, borderBottomWidth: 1 }
});