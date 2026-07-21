import React, { useState, useContext } from 'react';
import { View, Text, TextInput, TouchableOpacity, StyleSheet, Alert, ActivityIndicator } from 'react-native';
import { AuthContext } from '../context/AuthContext';
import { BASE_URL } from '../config';

export default function LoginScreen({ navigation }) {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [loading, setLoading] = useState(false);
  const { login, theme } = useContext(AuthContext);

  const handleLogin = async () => {
    if (!username || !password) return Alert.alert('Ошибка', 'Заполните все поля');
    setLoading(true);
    try {
      const response = await fetch(`${BASE_URL}/auth/login`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ username, password }) });
      if (response.ok) { const data = await response.json(); login(data.token); } else { Alert.alert('Ошибка', 'Неверный логин или пароль'); }
    } catch (error) { Alert.alert('Ошибка сети', 'Не удалось подключиться к серверу.'); } finally { setLoading(false); }
  };

  return (
    <View style={[styles.container, { backgroundColor: theme.bg }]}>
      <Text style={[styles.title, { color: theme.text }]}>System Monitor</Text>
      <Text style={[styles.subtitle, { color: theme.textMuted }]}>Вход в систему</Text>

      <TextInput style={[styles.input, { backgroundColor: theme.cardBg, color: theme.text, borderColor: theme.border }]} placeholder="Логин" placeholderTextColor={theme.textMuted} value={username} onChangeText={setUsername} autoCapitalize="none" />
      <TextInput style={[styles.input, { backgroundColor: theme.cardBg, color: theme.text, borderColor: theme.border }]} placeholder="Пароль" placeholderTextColor={theme.textMuted} value={password} onChangeText={setPassword} secureTextEntry />

      <TouchableOpacity style={[styles.button, { backgroundColor: theme.primary }]} onPress={handleLogin} disabled={loading}>
        {loading ? <ActivityIndicator color="#0B0E14" /> : <Text style={[styles.buttonText, { color: '#0B0E14' }]}>Войти</Text>}
      </TouchableOpacity>

      <TouchableOpacity onPress={() => navigation.navigate('Register')} style={{ marginTop: 20 }}>
        <Text style={[styles.linkText, { color: theme.textMuted }]}>Нет аккаунта? Создать</Text>
      </TouchableOpacity>
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, justifyContent: 'center', padding: 20 },
  title: { fontSize: 32, fontWeight: 'bold', marginBottom: 10, textAlign: 'center' },
  subtitle: { fontSize: 18, marginBottom: 30, textAlign: 'center' },
  input: { padding: 15, borderRadius: 10, marginBottom: 15, borderWidth: 1 },
  button: { padding: 15, borderRadius: 10, alignItems: 'center', marginTop: 10 },
  buttonText: { fontSize: 16, fontWeight: 'bold' },
  linkText: { textAlign: 'center', marginTop: 20, fontSize: 14 }
});