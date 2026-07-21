import React, { useEffect, useRef, useContext } from 'react';
import { View, Text, StyleSheet, Modal, TouchableOpacity, Animated, Dimensions } from 'react-native';
import { AuthContext } from '../context/AuthContext';

const { height } = Dimensions.get('window');

export const GlassModal = ({
  visible, title, children, onClose, onConfirm, confirmText = "CONFIRM", type = "default"
}) => {
  const { theme } = useContext(AuthContext);
  const fadeAnim = useRef(new Animated.Value(0)).current;
  const slideAnim = useRef(new Animated.Value(height)).current;

  useEffect(() => {
    if (visible) {
      Animated.parallel([
        Animated.timing(fadeAnim, { toValue: 1, duration: 300, useNativeDriver: true }),
        Animated.spring(slideAnim, { toValue: 0, tension: 50, friction: 9, useNativeDriver: true })
      ]).start();
    } else {
      Animated.timing(fadeAnim, { toValue: 0, duration: 200, useNativeDriver: true }).start();
      slideAnim.setValue(height);
    }
  }, [visible]);

  if (!visible) return null;

  const isDestructive = type === "destructive";

  return (
    <Modal transparent visible={visible} animationType="none">
      <Animated.View style={[styles.overlay, { opacity: fadeAnim, backgroundColor: theme.overlay }]}>
        <Animated.View style={[styles.glassContainer, { transform: [{ translateY: slideAnim }], backgroundColor: theme.cardBg, borderColor: theme.border }]}>
          <Text style={[styles.title, { color: theme.primary }]}>{title}</Text>
          <View style={styles.content}>{children}</View>
          <View style={styles.buttonRow}>
            <TouchableOpacity style={[styles.cancelBtn, { backgroundColor: theme.glassInput }]} onPress={onClose}>
              <Text style={[styles.cancelText, { color: theme.textMuted }]}>CANCEL</Text>
            </TouchableOpacity>
            <TouchableOpacity style={[styles.confirmBtn, { backgroundColor: isDestructive ? theme.danger : theme.primary }]} onPress={onConfirm}>
              <Text style={[styles.confirmText, { color: isDestructive ? '#FFF' : '#0B0E14' }]}>{confirmText}</Text>
            </TouchableOpacity>
          </View>
        </Animated.View>
      </Animated.View>
    </Modal>
  );
};

const styles = StyleSheet.create({
  overlay: { flex: 1, justifyContent: 'center', alignItems: 'center', padding: 20 },
  glassContainer: { width: '100%', borderRadius: 30, borderWidth: 1, padding: 25 },
  title: { fontSize: 22, fontWeight: '900', letterSpacing: 2, marginBottom: 20, textAlign: 'center' },
  content: { marginBottom: 25 },
  buttonRow: { flexDirection: 'row', gap: 15 },
  cancelBtn: { flex: 1, padding: 16, alignItems: 'center', borderRadius: 15 },
  cancelText: { fontWeight: 'bold', letterSpacing: 1 },
  confirmBtn: { flex: 1, padding: 16, borderRadius: 15, alignItems: 'center' },
  confirmText: { fontWeight: '900', letterSpacing: 1 }
});