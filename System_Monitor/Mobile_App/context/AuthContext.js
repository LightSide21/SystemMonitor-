import React, { createContext, useState, useEffect } from 'react';
import * as SecureStore from 'expo-secure-store';

export const AuthContext = createContext();

const darkTheme = {
  isDark: true, bg: '#0B0E14', cardBg: 'rgba(255, 255, 255, 0.03)', text: '#FFF',
  textMuted: '#8F9BB3', border: 'rgba(255, 255, 255, 0.08)', primary: '#32FF7E',
  danger: '#FF4757', glassInput: 'rgba(255, 255, 255, 0.05)', chartBg: '#1E232F', overlay: 'rgba(0,0,0,0.85)'
};

const lightTheme = {
  isDark: false, bg: '#F5F6F8', cardBg: 'rgba(0, 0, 0, 0.03)', text: '#0B0E14',
  textMuted: '#606F89', border: 'rgba(0, 0, 0, 0.08)', primary: '#2ED573',
  danger: '#FF4757', glassInput: 'rgba(0, 0, 0, 0.05)', chartBg: '#FFFFFF', overlay: 'rgba(255,255,255,0.7)'
};

export const AuthProvider = ({ children }) => {
  const [userToken, setUserToken] = useState(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isDarkMode, setIsDarkMode] = useState(true);

  const theme = isDarkMode ? darkTheme : lightTheme;
  const toggleTheme = () => setIsDarkMode(!isDarkMode);

  const checkToken = async () => {
    try {
      const token = await SecureStore.getItemAsync('jwtToken');
      if (token) setUserToken(token);
    } catch (e) { console.log('Ошибка при чтении токена', e); }
    setIsLoading(false);
  };

  useEffect(() => { checkToken(); }, []);

  const login = async (token) => {
    setUserToken(token);
    await SecureStore.setItemAsync('jwtToken', token);
  };

  const logout = async () => {
    setUserToken(null);
    await SecureStore.deleteItemAsync('jwtToken');
  };

  return (
    <AuthContext.Provider value={{ login, logout, userToken, isLoading, theme, toggleTheme }}>
      {children}
    </AuthContext.Provider>
  );
};