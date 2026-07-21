import React, { useEffect, useState, useContext } from 'react';
import { View, Text, StyleSheet, FlatList, TouchableOpacity, ActivityIndicator, ScrollView, Image } from 'react-native';
import { AuthContext } from '../context/AuthContext';
import { BASE_URL } from '../config';
import { GlassModal } from '../components/GlassModal';

const ActionButton = ({ title, iconSource, color, onPress, theme }) => (
  <TouchableOpacity style={[styles.actionBtn, { borderColor: theme.primary, backgroundColor: theme.glassInput }]} onPress={onPress}>
    <Image
      source={iconSource}
      style={{ width: 28, height: 28, marginBottom: 5 }}
      resizeMode="contain"
    />
    <Text style={{ fontSize: 10, fontWeight: '900', color }}>{title}</Text>
  </TouchableOpacity>
);

export default function ManagementScreen() {
  const { userToken, theme } = useContext(AuthContext);
  const [computers, setComputers] = useState([]);
  const [loading, setLoading] = useState(true);
  const [expandedPcId, setExpandedPcId] = useState(null);

  const [processes, setProcesses] = useState([]);
  const [loadingProcesses, setLoadingProcesses] = useState(false);
  const [killModalVisible, setKillModalVisible] = useState(false);
  const [selectedProc, setSelectedProc] = useState(null);
  const [cmdModalVisible, setCmdModalVisible] = useState(false);
  const [pendingCmd, setPendingCmd] = useState(null);

  const fetchComputers = async () => {
    setLoading(true);
    try {
      const response = await fetch(`${BASE_URL}/manage/computers`, { headers: { 'Authorization': `Bearer ${userToken}` } });
      if (response.ok) setComputers(await response.json());
    } finally { setLoading(false); }
  };

  useEffect(() => { fetchComputers(); }, []);

  const sendCommand = async (pcId, action, payload = '') => {
    try {
      const res = await fetch(`${BASE_URL}/manage/command?computerId=${pcId}&action=${action}&payload=${payload}`, { method: 'POST', headers: { 'Authorization': `Bearer ${userToken}` } });
      return res.ok;
    } catch (e) { return false; }
  };

  const requestProcesses = async (pcId) => {
    setLoadingProcesses(true);
    await sendCommand(pcId, 'GET_PROCESSES');
    setTimeout(async () => {
      try {
        const res = await fetch(`${BASE_URL}/manage/processes?computerId=${pcId}`, { headers: { 'Authorization': `Bearer ${userToken}` } });
        if (res.ok) setProcesses(JSON.parse(await res.text()));
      } finally { setLoadingProcesses(false); }
    }, 3000);
  };

  const renderComputer = ({ item }) => {
    const isExpanded = expandedPcId === item.id;
    return (
      <View style={[styles.glassCard, { backgroundColor: theme.cardBg, borderColor: isExpanded ? theme.primary : theme.border }]}>
        <TouchableOpacity style={styles.cardHeader} onPress={() => { setExpandedPcId(isExpanded ? null : item.id); setProcesses([]); }}>
          <View>
            <Text style={[styles.pcName, { color: theme.text }]}>{item.name || item.id}</Text>
            <Text style={[styles.status, { color: item.online ? theme.primary : theme.textMuted }]}>{item.online ? '🟢 ONLINE' : '⚪ OFFLINE'}</Text>
          </View>
          <Text style={{ color: theme.primary }}>{isExpanded ? '▲' : '▼'}</Text>
        </TouchableOpacity>

        {isExpanded && (
          <View style={[styles.panel, { backgroundColor: theme.glassInput }]}>
            <Text style={[styles.sectionTitle, { color: theme.textMuted }]}>УПРАВЛЕНИЕ ПИТАНИЕМ</Text>
            <View style={styles.actionGrid}>
                  <ActionButton
                    theme={theme} title="OFF"
                    iconSource={require('../ico/off.png')}
                    color={theme.danger}
                    onPress={() => { setPendingCmd({ id: item.id, act: 'SHUTDOWN' }); setCmdModalVisible(true); }}
                  />

                  <ActionButton
                    theme={theme} title="REBOOT"
                    iconSource={require('../ico/reboot.png')}
                    color={theme.primary}
                    onPress={() => { setPendingCmd({ id: item.id, act: 'REBOOT' }); setCmdModalVisible(true); }}
                  />

                  <ActionButton
                    theme={theme} title="SLEEP"
                    iconSource={require('../ico/sleep.png')}
                    color="#4A90E2"
                    onPress={() => { setPendingCmd({ id: item.id, act: 'SLEEP' }); setCmdModalVisible(true); }}
                  />

                  <ActionButton
                    theme={theme} title="LOCK"
                    iconSource={require('../ico/lock.png')}
                    color={theme.textMuted}
                    onPress={() => { setPendingCmd({ id: item.id, act: 'LOCK' }); setCmdModalVisible(true); }}
                  />
                </View>

            <View style={styles.procHeader}>
              <Text style={[styles.sectionTitle, { color: theme.textMuted, marginBottom: 0 }]}>ПРОЦЕССЫ (ТОП 100)</Text>
              <TouchableOpacity onPress={() => requestProcesses(item.id)}><Text style={{ color: theme.primary, fontWeight: 'bold' }}>↻ ОБНОВИТЬ</Text></TouchableOpacity>
            </View>

            {loadingProcesses ? <ActivityIndicator color={theme.primary} /> : (
              <ScrollView style={styles.procList} nestedScrollEnabled>
                {processes.map((p, i) => (
                  <TouchableOpacity key={i} style={[styles.procItem, { borderBottomColor: theme.border }]} onPress={() => { setSelectedProc({ pcId: item.id, ...p }); setKillModalVisible(true); }}>
                    <Text style={[styles.procName, { color: theme.text }]} numberOfLines={1}>{p.name}</Text>
                    <Text style={{ color: theme.primary, fontSize: 12, fontWeight: 'bold' }}>{p.memoryFormatted}</Text>
                  </TouchableOpacity>
                ))}
              </ScrollView>
            )}
          </View>
        )}
      </View>
    );
  };

  return (
    <View style={[styles.container, { backgroundColor: theme.bg }]}>
      <FlatList data={computers} renderItem={renderComputer} keyExtractor={t => t.id} />

      <GlassModal visible={cmdModalVisible} title="ACCEPT" onClose={() => setCmdModalVisible(false)} onConfirm={async () => { await sendCommand(pendingCmd.id, pendingCmd.act); setCmdModalVisible(false); }}>
        <Text style={{color: theme.text, textAlign: 'center'}}>Выполнить {pendingCmd?.act}?</Text>
      </GlassModal>

      <GlassModal visible={killModalVisible} title="ЗАВЕРШИТЬ ПРОЦЕСС" type="destructive" onClose={() => setKillModalVisible(false)} onConfirm={async () => { await sendCommand(selectedProc.pcId, 'KILL_PROCESS', selectedProc.id.toString()); setKillModalVisible(false); setProcesses(p => p.filter(x => x.id !== selectedProc.id)); }}>
        <Text style={{color: theme.text, textAlign: 'center'}}>Завершить {selectedProc?.name} (PID: {selectedProc?.id})?</Text>
      </GlassModal>
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, padding: 15, paddingTop: 50 },
  glassCard: { borderRadius: 20, marginBottom: 15, borderWidth: 1, overflow: 'hidden' },
  cardHeader: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', padding: 20 },
  pcName: { fontSize: 18, fontWeight: 'bold' },
  status: { fontSize: 10, fontWeight: '900', marginTop: 5 },
  panel: { padding: 20 },
  sectionTitle: { fontSize: 10, letterSpacing: 1, marginBottom: 15 },
  actionGrid: { flexDirection: 'row', justifyContent: 'space-between', marginBottom: 25 },
  actionBtn: { flex: 1, alignItems: 'center', padding: 12, marginHorizontal: 4, borderRadius: 12, borderWidth: 1 },
  procHeader: { flexDirection: 'row', justifyContent: 'space-between', marginTop: 10, marginBottom: 10 },
  procList: { maxHeight: 450 },
  procItem: { flexDirection: 'row', justifyContent: 'space-between', paddingVertical: 12, borderBottomWidth: 1 },
  procName: { fontSize: 13, flex: 1 }
});