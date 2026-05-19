import React, { useState, useContext } from 'react';
import { View, Text, TextInput, TouchableOpacity, StyleSheet, Alert } from 'react-native';
import { AuthContext } from '../context/AuthContext';
import { BASE_URL } from '../config';

export default function RegisterScreen({ navigation }) {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const { login, theme } = useContext(AuthContext);

  const handleRegister = async () => {
    if (!username || !password) return Alert.alert("Ошибка", "Заполните все поля");
    try {
      const response = await fetch(`${BASE_URL}/auth/register`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ username, password }) });
      const data = await response.json();
      if (response.ok) { login(data.token); } else { Alert.alert("Ошибка", data || "Регистрация не удалась"); }
    } catch (error) { Alert.alert("Ошибка сети", "Не удалось связаться с сервером"); }
  };

  return (
    <View style={[styles.container, { backgroundColor: theme.bg }]}>
      <Text style={[styles.title, { color: theme.text }]}>Регистрация</Text>

      <TextInput style={[styles.input, { backgroundColor: theme.cardBg, color: theme.text, borderColor: theme.border }]} placeholder="Придумайте логин" placeholderTextColor={theme.textMuted} value={username} onChangeText={setUsername} autoCapitalize="none" />
      <TextInput style={[styles.input, { backgroundColor: theme.cardBg, color: theme.text, borderColor: theme.border }]} placeholder="Придумайте пароль" placeholderTextColor={theme.textMuted} value={password} onChangeText={setPassword} secureTextEntry />

      <TouchableOpacity style={[styles.button, { backgroundColor: theme.primary }]} onPress={handleRegister}>
        <Text style={[styles.buttonText, { color: '#0B0E14' }]}>Создать аккаунт</Text>
      </TouchableOpacity>

      <TouchableOpacity onPress={() => navigation.navigate('Login')}>
        <Text style={[styles.linkText, { color: theme.textMuted }]}>Уже есть аккаунт? Войти</Text>
      </TouchableOpacity>
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, justifyContent: 'center', padding: 20 },
  title: { fontSize: 28, fontWeight: 'bold', marginBottom: 30, textAlign: 'center' },
  input: { padding: 15, borderRadius: 10, marginBottom: 15, borderWidth: 1 },
  button: { padding: 15, borderRadius: 10, alignItems: 'center', marginTop: 10 },
  buttonText: { fontSize: 16, fontWeight: 'bold' },
  linkText: { textAlign: 'center', marginTop: 20, fontSize: 14 }
});