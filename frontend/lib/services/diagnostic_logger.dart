import 'dart:convert';
// ignore: deprecated_member_use
import 'dart:html' as html;
import 'package:http/http.dart' as http;
import '../models/diagnostic_event.dart';

class DiagnosticLogger {
  static final DiagnosticLogger _instance = DiagnosticLogger._internal();
  factory DiagnosticLogger() => _instance;
  DiagnosticLogger._internal();

  final List<DiagnosticEvent> _buffer = [];
  final List<DiagnosticEvent> _unsyncedEvents = [];
  final int _maxBufferSize = 500;

  String? _userId;
  String? _sessionId;
  String? _correlationId;
  String? _callId;

  String? _apiBaseUrl;

  void init({required String apiBaseUrl}) {
    _apiBaseUrl = apiBaseUrl;
  }

  void setContext({String? userId, String? sessionId, String? correlationId, String? callId}) {
    if (userId != null) _userId = userId;
    if (sessionId != null) _sessionId = sessionId;
    if (correlationId != null) _correlationId = correlationId;
    if (callId != null) _callId = callId;
  }

  void clearSession() {
    _buffer.clear();
    _unsyncedEvents.clear();
    _userId = null;
    _sessionId = null;
    _correlationId = null;
    _callId = null;
  }

  String _sanitize(String input) {
    var sanitized = input.replaceAll(RegExp(r'eyJ[a-zA-Z0-9_-]+\.[a-zA-Z0-9_-]+\.[a-zA-Z0-9_-]+'), '[REDACTED_JWT]');
    sanitized = sanitized.replaceAll(RegExp(r'(Bearer\s+)[^\s"\}]+', caseSensitive: false), r'$1[REDACTED]');
    sanitized = sanitized.replaceAll(RegExp(r'(password["\s:=]+)[^\s,}]+', caseSensitive: false), r'$1[REDACTED]');
    sanitized = sanitized.replaceAll(RegExp(r'(credential["\s:=]+)[^\s,}]+', caseSensitive: false), r'$1[REDACTED]');
    sanitized = sanitized.replaceAll(RegExp(r'(secret["\s:=]+)[^\s,}]+', caseSensitive: false), r'$1[REDACTED]');
    sanitized = sanitized.replaceAll(RegExp(r'a=ice-pwd:[^\s\\]+'), 'a=ice-pwd:[REDACTED]');
    sanitized = sanitized.replaceAll(RegExp(r'a=ice-ufrag:[^\s\\]+'), 'a=ice-ufrag:[REDACTED]');
    sanitized = sanitized.replaceAll(RegExp(r'cookie[:=]\s*[^\s;"]+'), 'cookie: [REDACTED]');
    return sanitized;
  }

  Map<String, dynamic>? _sanitizeMetadata(Map<String, dynamic>? metadata) {
    if (metadata == null) return null;
    final sanitizedMap = <String, dynamic>{};
    for (final entry in metadata.entries) {
      final key = entry.key.toLowerCase();
      if (key.contains('password') || key.contains('token') || key.contains('secret') || key.contains('credential') || key.contains('cookie')) {
        sanitizedMap[entry.key] = '[REDACTED]';
      } else if (entry.value is String) {
        sanitizedMap[entry.key] = _sanitize(entry.value as String);
      } else {
        sanitizedMap[entry.key] = entry.value;
      }
    }
    return sanitizedMap;
  }

  void log(String message, {String severity = 'Info', String component = 'Frontend', String eventName = 'Log', Map<String, dynamic>? metadata}) {
    final event = DiagnosticEvent(
      id: DateTime.now().millisecondsSinceEpoch.toString() + '_' + _buffer.length.toString(),
      timestamp: DateTime.now().toUtc().toIso8601String(),
      severity: severity,
      component: component,
      eventName: eventName,
      message: _sanitize(message),
      userId: _userId,
      sessionId: _sessionId,
      correlationId: _correlationId,
      callId: _callId,
      metadata: _sanitizeMetadata(metadata),
    );

    _buffer.add(event);
    if (_buffer.length > _maxBufferSize) {
      _buffer.removeAt(0);
    }

    _unsyncedEvents.add(event);

    // Auto-sync if unsynced batch gets too large
    if (_unsyncedEvents.length >= 50) {
      syncClientLogs();
    }
  }

  List<DiagnosticEvent> getLogs() {
    return List.unmodifiable(_buffer);
  }

  Future<void> syncClientLogs() async {
    final token = html.window.localStorage['connect_token'];
    if (_apiBaseUrl == null) {
      print('DiagnosticLogger Error: apiBaseUrl not initialized. Cannot sync logs.');
      return;
    }
    if (_unsyncedEvents.isEmpty || token == null || token.isEmpty) return;

    // Take a snapshot of current unsynced events
    final logsToSync = List<DiagnosticEvent>.from(_unsyncedEvents);
    _unsyncedEvents.clear();

    try {
      final response = await http.post(
        Uri.parse('$_apiBaseUrl/diagnostics/client-logs'),
        headers: {
          'Content-Type': 'application/json',
          'Authorization': 'Bearer $token',
        },
        body: jsonEncode(logsToSync.map((e) => e.toJson()).toList()),
      );

      if (response.statusCode != 200 && response.statusCode != 204) {
        // If it failed, put them back (if we haven't overflowed)
        if (_unsyncedEvents.length < _maxBufferSize) {
           _unsyncedEvents.insertAll(0, logsToSync);
        }
      }
    } catch (e) {
      // If it failed, put them back
      if (_unsyncedEvents.length < _maxBufferSize) {
         _unsyncedEvents.insertAll(0, logsToSync);
      }
    }
  }
}
