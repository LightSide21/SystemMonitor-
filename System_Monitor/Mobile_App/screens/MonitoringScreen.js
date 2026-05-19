import React, { useContext, useEffect, useState } from 'react';
import { View, Text, StyleSheet, FlatList, TouchableOpacity, ActivityIndicator, StatusBar, TextInput, Alert } from 'react-native';
import { useNavigation } from '@react-navigation/native';
import { AuthContext } from '../context/AuthContext';
import { BASE_URL } from '../config';
import { GlassModal } from '../components/GlassModal'; //  модальное окно

export default function MonitoringScreen() {
  const { logout, userToken, theme, toggleTheme } = useContext(AuthContext);
  const [computers, setComputers] = useState([]);
  const [loading, setLoading] = useState(true);
  const [showAddInput, setShowAddInput] = useState(false);
  const [connectionCode, setConnectionCode] = useState('');
  const [animatedPlaceholder, setAnimatedPlaceholder] = useState('');

  const navigation = useNavigation();

  const fetchComputers = async () => {
    setLoading(true);
    try {
      const response = await fetch(`${BASE_URL}/manage/computers`, {
        method: 'GET', headers: { 'Authorization': `Bearer ${userToken}` }
      });
      if (response.ok) {
        const data = await response.json();
        setComputers(Array.isArray(data) ? data : []);
      }
    } catch (error) { setComputers([]); } finally { setLoading(false); }
  };

  useEffect(() => { fetchComputers(); }, []);

  useEffect(() => {
    if (showAddInput) {
      setAnimatedPlaceholder('');
      const fullText = "XXX-XXX";
      let i = 0;
      const typingInterval = setInterval(() => {
        if (i < fullText.length) { setAnimatedPlaceholder((prev) => prev + fullText.charAt(i)); i++; }
        else { clearInterval(typingInterval); }
      }, 70);
      return () => clearInterval(typingInterval);
    } else {
      setAnimatedPlaceholder('');
      setConnectionCode('');
    }
  }, [showAddInput]);

  const handleCodeChange = (text) => {
    let cleaned = text.replace(/[^a-zA-Z0-9]/g, '').toUpperCase();
    if (cleaned.length > 3) cleaned = cleaned.slice(0, 3) + '-' + cleaned.slice(3, 6);
    setConnectionCode(cleaned);
  };

  const handleLink = async () => {
    if (!connectionCode || connectionCode.length < 7) return Alert.alert("Ошибка", "Введите полный код (6 символов)");
    try {
      const response = await fetch(`${BASE_URL}/manage/computers/link?connectionCode=${connectionCode}`, {
        method: 'POST', headers: { 'Authorization': `Bearer ${userToken}` }
      });
      const message = await response.text();
      if (response.ok) {
        Alert.alert("Успех", "Узел авторизован");
        setConnectionCode('');
        setShowAddInput(false);
        fetchComputers();
      } else { Alert.alert("Ошибка", message); }
    } catch (e) { Alert.alert("Ошибка", "Нет связи с сервером"); }
  };

  const deleteComputer = (computerId) => {
    Alert.alert("Удаление устройства", "Отвязать это устройство?", [
      { text: "Отмена", style: "cancel" },
      { text: "Удалить", style: "destructive", onPress: async () => {
          try {
            const response = await fetch(`${BASE_URL}/manage/computers/${computerId}`, {
              method: 'DELETE', headers: { 'Authorization': `Bearer ${userToken}` }
            });
            if (response.ok) { fetchComputers(); } else { Alert.alert("Ошибка", "Не удалось удалить узел"); }
          } catch (e) { Alert.alert("Ошибка сети", "Сервер недоступен"); }
        }
      }
    ]);
  };

  return (
    <View style={[styles.container, { backgroundColor: theme.bg }]}>
      <StatusBar barStyle={theme.isDark ? "light-content" : "dark-content"} />

      <TouchableOpacity style={[styles.floatingThemeToggle, { backgroundColor: theme.cardBg, borderColor: theme.border }]} onPress={toggleTheme}>
        <Text style={{ fontSize: 18 }}>{theme.isDark ? '🌙' : '☀️'}</Text>
      </TouchableOpacity>

      {/* ОКНО ПОДКЛЮЧЕНИЯ */}
      <GlassModal
        visible={showAddInput}
        title="ПОДКЛЮЧИТЬ УСТРОЙСТВО"
        onClose={() => setShowAddInput(false)}
        onConfirm={handleLink}
        confirmText="CONNECT"
      >
        <TextInput
          style={[styles.modalInput, { backgroundColor: theme.bg, color: theme.text, borderColor: theme.primary }]}
          placeholder={animatedPlaceholder}
          placeholderTextColor={theme.textMuted}
          value={connectionCode}
          onChangeText={handleCodeChange}
          maxLength={7}
          autoCapitalize="characters"
          textAlign="center"
        />
        <Text style={{ color: theme.textMuted, textAlign: 'center', marginTop: 15, fontSize: 12 }}>
          Введите код из клиентского приложения для привязки компьютера к вашему аккаунту.
        </Text>
      </GlassModal>

      <FlatList
        data={computers}
        keyExtractor={(item, index) => item?.id ? item.id.toString() : index.toString()}
        renderItem={({ item }) => (
          <TouchableOpacity
            style={[styles.glassCard, { backgroundColor: theme.cardBg, borderColor: theme.border }]}
            onPress={() => navigation.navigate('Детали ПК', { computer: item })}
            onLongPress={() => deleteComputer(item.id)}
            activeOpacity={0.7}
          >
            <View style={styles.cardHeader}>
              <Text style={[styles.pcName, { color: theme.text }]}>{item?.name || 'Core Node'}</Text>
              <View style={[styles.statusDot, { backgroundColor: item?.online ? theme.primary : theme.textMuted }]} />
            </View>
            <View style={styles.cardDivider} />
            <Text style={[styles.cardLabel, { color: theme.textMuted }]}>ID: <Text style={{color: theme.text}}>{item?.id || 'Unknown'}</Text></Text>
            {item?.online && <View style={[styles.onlineBadge, { backgroundColor: theme.glassInput }]}><Text style={[styles.onlineText, { color: theme.primary }]}>ACTIVE</Text></View>}
          </TouchableOpacity>
        )}
        contentContainerStyle={{ padding: 20, paddingTop: 100, paddingBottom: 100 }}
        onRefresh={fetchComputers} refreshing={loading}
      />

      <TouchableOpacity style={[styles.floatingAddButton, { backgroundColor: theme.primary, shadowColor: theme.primary }]} onPress={() => setShowAddInput(true)}>
        <Text style={styles.addIcon}>+</Text>
      </TouchableOpacity>

      <TouchableOpacity style={[styles.logoutBtn, { borderColor: theme.danger }]} onPress={logout}>
        <Text style={[styles.logoutText, { color: theme.danger }]}>ВЫЙТИ</Text>
      </TouchableOpacity>
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1 },
  floatingThemeToggle: { position: 'absolute', top: 50, right: 20, width: 44, height: 44, borderRadius: 22, justifyContent: 'center', alignItems: 'center', borderWidth: 1, zIndex: 10 },

  // Стили для  внутри модального окна
  modalInput: { height: 55, fontSize: 24, fontWeight: 'bold', paddingHorizontal: 15, letterSpacing: 4, borderRadius: 12, borderWidth: 1 },

  floatingAddButton: { position: 'absolute', bottom: 110, right: 20, width: 56, height: 56, borderRadius: 28, justifyContent: 'center', alignItems: 'center', elevation: 8, shadowOpacity: 0.6, shadowRadius: 10, zIndex: 10 },
  addIcon: { color: '#0B0E14', fontSize: 32, fontWeight: 'bold' },
  glassCard: { borderRadius: 20, padding: 20, marginBottom: 20, borderWidth: 1 },
  cardHeader: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  pcName: { fontSize: 18, fontWeight: '900' },
  statusDot: { width: 10, height: 10, borderRadius: 5 },
  cardDivider: { height: 1, backgroundColor: 'rgba(143, 155, 179, 0.15)', marginVertical: 12 },
  cardLabel: { fontSize: 10, letterSpacing: 1 },
  onlineBadge: { position: 'absolute', right: 20, bottom: 20, paddingHorizontal: 8, paddingVertical: 2, borderRadius: 4 },
  onlineText: { fontSize: 9, fontWeight: '900' },
  logoutBtn: { margin: 20, padding: 15, borderRadius: 12, borderWidth: 1, alignItems: 'center', marginBottom: 40 },
  logoutText: { fontWeight: '900', letterSpacing: 2 }
});