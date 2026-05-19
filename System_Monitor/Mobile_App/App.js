import React, { useContext } from 'react';
import { ActivityIndicator, View, Image } from 'react-native';
import { NavigationContainer } from '@react-navigation/native';
import { createNativeStackNavigator } from '@react-navigation/native-stack';
import { createBottomTabNavigator } from '@react-navigation/bottom-tabs';

import { AuthProvider, AuthContext } from './src/context/AuthContext';
import LoginScreen from './src/screens/LoginScreen';
import RegisterScreen from './src/screens/RegisterScreen';
import MonitoringScreen from './src/screens/MonitoringScreen';
import ComputerDetailsScreen from './src/screens/ComputerDetailsScreen';
import ManagementScreen from './src/screens/ManagementScreen';

const Stack = createNativeStackNavigator();
const Tab = createBottomTabNavigator();


function MonitoringStack() {
  const { theme } = useContext(AuthContext);
  return (
    <Stack.Navigator
      screenOptions={{
        headerStyle: { backgroundColor: theme.bg },
        headerTintColor: theme.primary,
        headerTitleStyle: { fontWeight: 'bold', color: theme.text },
      }}
    >
      <Stack.Screen name="Список ПК" component={MonitoringScreen} options={{ headerShown: false }} />
      <Stack.Screen name="Детали ПК" component={ComputerDetailsScreen} options={{ title: 'Метрики системы' }} />
    </Stack.Navigator>
  );
}

function MainTabs() {
  const { theme } = useContext(AuthContext);
  return (
    <Tab.Navigator
      screenOptions={{
        headerShown: false,
        tabBarStyle: {
          backgroundColor: theme.bg,
          borderTopWidth: 1,
          borderTopColor: theme.border,
          height: 60,
          paddingBottom: 15,
          paddingTop: 5,
        },
        tabBarActiveTintColor: theme.primary,
        tabBarInactiveTintColor: theme.textMuted,
      }}
    >
      <Tab.Screen
        name="MonitoringTab"
        component={MonitoringStack}
        options={{
          tabBarLabel: 'Метрики',
          tabBarIcon: ({ color, size }) => (
            <Image
              source={require('./src/ico/monitor.png')} 
              style={{ width: size, height: size, tintColor: color }}
              resizeMode="contain"
            />
          )
        }}
      />
      <Tab.Screen
        name="Management"
        component={ManagementScreen}
        options={{
          tabBarLabel: 'Управление',
          tabBarIcon: ({ color, size }) => (
            <Image
              source={require('./src/ico/manage.png')} 
              style={{ width: size, height: size, tintColor: color }}
              resizeMode="contain"
            />
          )
        }}
      />
    </Tab.Navigator>
  );
}

function AppNav() {
  const { isLoading, userToken, theme } = useContext(AuthContext);

  if (isLoading) {
    return (
      <View style={{ flex: 1, backgroundColor: theme?.bg || '#0B0E14', justifyContent: 'center' }}>
        <ActivityIndicator size="large" color={theme?.primary || '#32FF7E'} />
      </View>
    );
  }

  return (
    <NavigationContainer>
      <Stack.Navigator screenOptions={{ headerShown: false }}>
        {userToken == null ? (
          <>
            <Stack.Screen name="Login" component={LoginScreen} />
            <Stack.Screen name="Register" component={RegisterScreen} />
          </>
        ) : (
          <Stack.Screen name="MainTabs" component={MainTabs} />
        )}
      </Stack.Navigator>
    </NavigationContainer>
  );
}

export default function App() {
  return <AuthProvider><AppNav /></AuthProvider>;
}