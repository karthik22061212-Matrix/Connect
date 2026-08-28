// ignore_for_file: deprecated_member_use, unused_element

import 'dart:async';
import 'dart:convert';
import 'dart:html' as html;
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:signalr_netcore/signalr_client.dart';

void main() {
  runApp(const ConnectApp());
}

class ConnectApp extends StatelessWidget {
  const ConnectApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Connect',
      debugShowCheckedModeBanner: false,
      theme: ThemeData(
        useMaterial3: true,
        fontFamily: 'Roboto',
        colorScheme: ColorScheme.fromSeed(
          seedColor: const Color(0xFF0D9488), // Emerald Teal accent
          primary: const Color(0xFF0D9488),
          onPrimary: Colors.white,
          secondary: const Color(0xFF0F766E),
          surface: Colors.white,
          surfaceContainerLowest: const Color(0xFFF8FAFC),
          error: const Color(0xFFE11D48),
        ),
        scaffoldBackgroundColor: const Color(0xFFF8FAFC),
        appBarTheme: const AppBarTheme(
          backgroundColor: Colors.white,
          elevation: 0.5,
          scrolledUnderElevation: 1.0,
          centerTitle: false,
          titleTextStyle: TextStyle(
            color: Color(0xFF0F172A),
            fontSize: 20,
            fontWeight: FontWeight.bold,
          ),
          iconTheme: IconThemeData(color: Color(0xFF0F172A)),
        ),
        cardTheme: CardThemeData(
          color: Colors.white,
          elevation: 0,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(16),
            side: const BorderSide(color: Color(0xFFE2E8F0), width: 1),
          ),
        ),
        inputDecorationTheme: InputDecorationTheme(
          filled: true,
          fillColor: Colors.white,
          contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
          border: OutlineInputBorder(
            borderRadius: BorderRadius.circular(12),
            borderSide: const BorderSide(color: Color(0xFFCBD5E1)),
          ),
          enabledBorder: OutlineInputBorder(
            borderRadius: BorderRadius.circular(12),
            borderSide: const BorderSide(color: Color(0xFFE2E8F0)),
          ),
          focusedBorder: OutlineInputBorder(
            borderRadius: BorderRadius.circular(12),
            borderSide: const BorderSide(color: Color(0xFF0D9488), width: 2),
          ),
          errorBorder: OutlineInputBorder(
            borderRadius: BorderRadius.circular(12),
            borderSide: const BorderSide(color: Color(0xFFE11D48)),
          ),
        ),
        elevatedButtonTheme: ElevatedButtonThemeData(
          style: ElevatedButton.styleFrom(
            backgroundColor: const Color(0xFF0D9488),
            foregroundColor: Colors.white,
            elevation: 0,
            padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 14),
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(12),
            ),
            textStyle: const TextStyle(fontWeight: FontWeight.w600, fontSize: 15),
          ),
        ),
        outlinedButtonTheme: OutlinedButtonThemeData(
          style: OutlinedButton.styleFrom(
            foregroundColor: const Color(0xFF475569),
            side: const BorderSide(color: Color(0xFFCBD5E1)),
            padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 12),
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(12),
            ),
          ),
        ),
      ),
      home: const MainConsumerDashboard(),
    );
  }
}

class UserSession {
  final String token;
  final String id;
  final String email;
  final String handle;

  UserSession({
    required this.token,
    required this.id,
    required this.email,
    required this.handle,
  });

  factory UserSession.fromJson(Map<String, dynamic> json) {
    return UserSession(
      token: json['token'] ?? '',
      id: json['id'] ?? '',
      email: json['email'] ?? '',
      handle: json['userId'] ?? '',
    );
  }
}

class MainConsumerDashboard extends StatefulWidget {
  const MainConsumerDashboard({super.key});

  @override
  State<MainConsumerDashboard> createState() => _MainConsumerDashboardState();
}

class _MainConsumerDashboardState extends State<MainConsumerDashboard> {
  static const String _baseUrl = String.fromEnvironment(
    'API_BASE_URL',
    defaultValue: 'http://localhost:5200',
  );

  // DEV NOTE: Dual user slot switcher (_user1Session vs _user2Session) is for dev/e2e test harness
  // convenience only on web. Will be removed once single-account-per-device persistent auth is standard.
  UserSession? _user1Session;
  UserSession? _user2Session;
  int _activeSessionIndex = 1;

  UserSession? get currentSession => _activeSessionIndex == 1 ? _user1Session : _user2Session;

  int _selectedNavIndex = 0;

  HubConnection? _hubConnection;
  bool _isHubConnected = false;

  final TextEditingController _searchQueryController = TextEditingController();
  List<dynamic> _searchResults = [];
  bool _isSearching = false;

  List<dynamic> _pendingRequests = [];
  List<dynamic> _sentRequests = [];
  List<dynamic> _connections = [];
  List<dynamic> _blockedUsers = [];
  List<dynamic> _callHistory = [];

  final FocusNode _loginEmailFocusNode = FocusNode();
  final FocusNode _loginPasswordFocusNode = FocusNode();

  final FocusNode _regHandleFocusNode = FocusNode();
  final FocusNode _regEmailFocusNode = FocusNode();
  final FocusNode _regPasswordFocusNode = FocusNode();
  final FocusNode _regConfirmPasswordFocusNode = FocusNode();

  String? _loginEmailError;
  String? _loginPasswordError;

  String? _regHandleError;
  String? _regEmailError;
  String? _regPasswordError;
  String? _regConfirmPasswordError;

  String? _handleCheckResult;
  bool? _isHandleAvailable;

  final TextEditingController _regEmailController = TextEditingController();
  final TextEditingController _regPasswordController = TextEditingController();
  final TextEditingController _regConfirmPasswordController = TextEditingController();
  final TextEditingController _regHandleController = TextEditingController();
  final TextEditingController _regPhoneController = TextEditingController();

  final TextEditingController _loginEmailController = TextEditingController();
  final TextEditingController _loginPasswordController = TextEditingController();

  bool _isAuthModeLogin = true;
  String? _authErrorMessage;
  String? _authSuccessMessage;
  bool _isAuthLoading = false;

  bool _obscureLoginPassword = true;
  bool _obscureRegPassword = true;
  bool _obscureRegConfirmPassword = true;

  String _callStatusText = '';
  String? _activeCallId;
  String? _callerOrCalleeName;
  bool _isIncomingCall = false;
  bool _isActiveCall = false;
  bool _isRinging = false;
  int _callTimerSeconds = 0;
  Timer? _callTimer;
  int _ringTimerSeconds = 15;
  Timer? _ringTimer;

  final TextEditingController _reportReasonController = TextEditingController();
  final TextEditingController _reportNoteController = TextEditingController();

  final List<String> _consoleLogs = [];
  bool _showDevConsole = false;

  void _log(String message) {
    final timestamp = DateTime.now().toIso8601String().substring(11, 19);
    setState(() {
      _consoleLogs.insert(0, '[$timestamp] $message');
    });
  }

  Timer? _expiryTimer;

  String _decodeBase64(String str) {
    String output = str.replaceAll('-', '+').replaceAll('_', '/');
    switch (output.length % 4) {
      case 0:
        break;
      case 2:
        output += '==';
        break;
      case 3:
        output += '=';
        break;
      default:
        throw Exception('Illegal base64url string!');
    }
    return utf8.decode(base64.decode(output));
  }

  DateTime? _getJwtExpiry(String token) {
    try {
      final parts = token.split('.');
      if (parts.length != 3) return null;
      final payloadString = _decodeBase64(parts[1]);
      final Map<String, dynamic> payload = jsonDecode(payloadString);
      if (payload.containsKey('exp')) {
        final exp = payload['exp'];
        int? expSeconds;
        if (exp is int) {
          expSeconds = exp;
        } else if (exp is double) {
          expSeconds = exp.toInt();
        } else if (exp is String) {
          expSeconds = int.tryParse(exp);
        }
        if (expSeconds != null) {
          return DateTime.fromMillisecondsSinceEpoch(expSeconds * 1000, isUtc: true);
        }
      }
    } catch (e) {
      _log('Error parsing JWT expiry: $e');
    }
    return null;
  }

  bool _scheduleExpiryTimer(String? token) {
    _expiryTimer?.cancel();
    _expiryTimer = null;

    if (token == null || token.isEmpty) return false;

    final expiryDate = _getJwtExpiry(token);
    if (expiryDate == null) return false;

    final remaining = expiryDate.difference(DateTime.now().toUtc());
    _log('JWT expiry time: ${expiryDate.toIso8601String()} (remaining: ${remaining.inSeconds}s)');

    if (remaining.inMilliseconds <= 0) {
      _log('JWT token is already expired. Triggering immediate logout cleanup.');
      _handle401();
      return true;
    } else {
      _expiryTimer = Timer(remaining, () {
        _log('Proactive JWT expiry timer fired after ${remaining.inSeconds}s idle.');
        _handle401();
      });
      return false;
    }
  }

  // --- LocalStorage Session Persistence Helpers ---
  void _saveSessionToLocalStorage(UserSession session) {
    try {
      html.window.localStorage['connect_token'] = session.token;
      html.window.localStorage['connect_id'] = session.id;
      html.window.localStorage['connect_email'] = session.email;
      html.window.localStorage['connect_handle'] = session.handle;
    } catch (e) {
      _log('Error saving session to localStorage: $e');
    }
  }

  void _loadSessionFromLocalStorage() {
    try {
      final token = html.window.localStorage['connect_token'];
      final id = html.window.localStorage['connect_id'];
      final email = html.window.localStorage['connect_email'];
      final handle = html.window.localStorage['connect_handle'];

      if (token != null && token.isNotEmpty && handle != null && handle.isNotEmpty) {
        final isExpired = _scheduleExpiryTimer(token);
        if (isExpired) {
          _log('Stored session token was expired on load. Cleared and returned to login.');
          return;
        }

        final restoredSession = UserSession(
          token: token,
          id: id ?? '',
          email: email ?? '',
          handle: handle,
        );
        setState(() {
          _user1Session = restoredSession;
          _activeSessionIndex = 1;
        });
        _log('Restored session for @$handle from localStorage');
        _connectSignalR();
        _refreshActiveTabData();
      }
    } catch (e) {
      _log('Error loading session from localStorage: $e');
    }
  }

  void _clearSessionFromLocalStorage() {
    try {
      html.window.localStorage.remove('connect_token');
      html.window.localStorage.remove('connect_id');
      html.window.localStorage.remove('connect_email');
      html.window.localStorage.remove('connect_handle');
    } catch (e) {
      _log('Error clearing session from localStorage: $e');
    }
  }

  void _handle401() {
    _expiryTimer?.cancel();
    _expiryTimer = null;
    _clearSessionFromLocalStorage();
    _hubConnection?.stop();
    setState(() {
      _user1Session = null;
      _user2Session = null;
      _activeSessionIndex = 1;
      _isHubConnected = false;
      _authSuccessMessage = null;
      _authErrorMessage = null;
    });
    if (mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Session expired. Please log in again.'),
          behavior: SnackBarBehavior.floating,
        ),
      );
    }
  }

  void _logout() {
    _expiryTimer?.cancel();
    _expiryTimer = null;
    _clearSessionFromLocalStorage();
    _hubConnection?.stop();
    setState(() {
      if (_activeSessionIndex == 1) {
        _user1Session = null;
      } else {
        _user2Session = null;
      }
      _isHubConnected = false;
      _authSuccessMessage = 'Logged out successfully.';
      _authErrorMessage = null;
    });
    _log('Logged out and cleared localStorage session');
  }

  @override
  void initState() {
    super.initState();

    _loadSessionFromLocalStorage();

    _loginEmailFocusNode.addListener(() {
      if (!_loginEmailFocusNode.hasFocus) {
        setState(() {
          _loginEmailError = _validateLoginEmailOrUserId(_loginEmailController.text);
        });
      }
    });

    _loginPasswordFocusNode.addListener(() {
      if (!_loginPasswordFocusNode.hasFocus) {
        setState(() {
          _loginPasswordError = _validateLoginPassword(_loginPasswordController.text);
        });
      }
    });

    _regHandleFocusNode.addListener(() {
      if (!_regHandleFocusNode.hasFocus) {
        setState(() {
          _regHandleError = _validateRegUserId(_regHandleController.text);
        });
        if (_regHandleController.text.trim().isNotEmpty) {
          _checkHandleAvailability();
        } else {
          setState(() {
            _handleCheckResult = null;
            _isHandleAvailable = null;
          });
        }
      }
    });

    _regHandleController.addListener(() {
      if (_handleCheckResult != null) {
        setState(() {
          _handleCheckResult = null;
          _isHandleAvailable = null;
        });
      }
    });

    _regEmailFocusNode.addListener(() {
      if (!_regEmailFocusNode.hasFocus) {
        setState(() {
          _regEmailError = _validateRegEmail(_regEmailController.text);
        });
      }
    });

    _regPasswordFocusNode.addListener(() {
      if (!_regPasswordFocusNode.hasFocus) {
        setState(() {
          _regPasswordError = _validateRegPassword(_regPasswordController.text);
        });
      }
    });

    _regConfirmPasswordFocusNode.addListener(() {
      if (!_regConfirmPasswordFocusNode.hasFocus) {
        setState(() {
          _regConfirmPasswordError = _validateRegConfirmPassword(_regConfirmPasswordController.text);
        });
      }
    });
  }

  @override
  void dispose() {
    _expiryTimer?.cancel();
    _expiryTimer = null;
    _hubConnection?.stop();
    _callTimer?.cancel();
    _ringTimer?.cancel();
    _searchQueryController.dispose();
    _regEmailController.dispose();
    _regPasswordController.dispose();
    _regConfirmPasswordController.dispose();
    _regHandleController.dispose();
    _regPhoneController.dispose();
    _loginEmailController.dispose();
    _loginPasswordController.dispose();
    _loginEmailFocusNode.dispose();
    _loginPasswordFocusNode.dispose();
    _regHandleFocusNode.dispose();
    _regEmailFocusNode.dispose();
    _regPasswordFocusNode.dispose();
    _regConfirmPasswordFocusNode.dispose();
    _reportReasonController.dispose();
    _reportNoteController.dispose();
    super.dispose();
  }

  void _clearAuthErrorsAndFields() {
    setState(() {
      _authErrorMessage = null;
      _authSuccessMessage = null;
      _loginEmailError = null;
      _loginPasswordError = null;
      _regHandleError = null;
      _regEmailError = null;
      _regPasswordError = null;
      _regConfirmPasswordError = null;
      _loginEmailController.clear();
      _loginPasswordController.clear();
      _regHandleController.clear();
      _regEmailController.clear();
      _regPasswordController.clear();
      _regConfirmPasswordController.clear();
      _regPhoneController.clear();
      _handleCheckResult = null;
      _isHandleAvailable = null;
      _obscureLoginPassword = true;
      _obscureRegPassword = true;
      _obscureRegConfirmPassword = true;
    });
  }

  String? _validateLoginEmailOrUserId(String? value) {
    final input = value?.trim() ?? '';
    if (input.isEmpty) {
      return 'Please enter your email or user ID.';
    }
    if (input.contains('@')) {
      final emailRegExp = RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$');
      if (!emailRegExp.hasMatch(input)) {
        return "That doesn't look like a valid email address.";
      }
    }
    return null;
  }

  String? _validateLoginPassword(String? value) {
    final input = value ?? '';
    if (input.isEmpty) {
      return 'Please enter your password.';
    }
    return null;
  }

  String? _validateRegUserId(String? value) {
    final input = value?.trim() ?? '';
    if (input.isEmpty) {
      return 'Please choose a user ID.';
    }
    return null;
  }

  String? _validateRegEmail(String? value) {
    final input = value?.trim() ?? '';
    if (input.isEmpty) {
      return 'Please enter your email address.';
    }
    final emailRegExp = RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$');
    if (!emailRegExp.hasMatch(input)) {
      return "That doesn't look like a valid email address.";
    }
    return null;
  }

  String? _validateRegPassword(String? value) {
    final input = value ?? '';
    if (input.isEmpty) {
      return 'Please enter your password.';
    }
    if (input.length < 8) {
      return 'Password must be at least 8 characters.';
    }
    return null;
  }

  String? _validateRegConfirmPassword(String? value) {
    if (value != _regPasswordController.text) {
      return "Passwords don't match.";
    }
    return null;
  }

  Future<void> _connectSignalR() async {
    final session = currentSession;
    if (session == null) return;

    if (_hubConnection != null) {
      await _hubConnection!.stop();
      _hubConnection = null;
    }

    final hubUrl = '$_baseUrl/hubs/call?access_token=${session.token}';
    _hubConnection = HubConnectionBuilder()
        .withUrl(hubUrl)
        .withAutomaticReconnect()
        .build();

    _hubConnection!.on('UserPresenceChanged', (args) {
      _log('SignalR Event: UserPresenceChanged -> $args');
      _fetchConnections();
    });

    _hubConnection!.on('IncomingCall', (args) {
      _log('SignalR Event: IncomingCall -> $args');
      if (args != null && args.length >= 3) {
        final callId = args[0].toString();
        final callerHandle = args[2].toString();

        setState(() {
          _activeCallId = callId;
          _callerOrCalleeName = callerHandle;
          _isIncomingCall = true;
          _isRinging = false;
          _isActiveCall = false;
          _ringTimerSeconds = 15;
          _callStatusText = 'Incoming call from $callerHandle';
        });

        _startRingCountdown();
      }
    });

    _hubConnection!.on('CallAccepted', (args) {
      _log('SignalR Event: CallAccepted -> $args');
      _ringTimer?.cancel();
      setState(() {
        _isRinging = false;
        _isIncomingCall = false;
        _isActiveCall = true;
        _callStatusText = 'Call Active';
        _callTimerSeconds = 0;
      });
      _startCallTimer();
    });

    _hubConnection!.on('CallRejected', (args) {
      _log('SignalR Event: CallRejected -> $args');
      _ringTimer?.cancel();
      _callTimer?.cancel();
      setState(() {
        _isRinging = false;
        _isIncomingCall = false;
        _isActiveCall = false;
        _activeCallId = null;
        _callStatusText = 'Call Rejected by recipient';
      });
      _fetchCallHistory();
    });

    _hubConnection!.on('CallEnded', (args) {
      _log('SignalR Event: CallEnded -> $args');
      _ringTimer?.cancel();
      _callTimer?.cancel();
      setState(() {
        _isRinging = false;
        _isIncomingCall = false;
        _isActiveCall = false;
        _activeCallId = null;
        _callStatusText = 'Call Ended';
      });
      _fetchCallHistory();
    });

    _hubConnection!.on('CallTimeout', (args) {
      _log('SignalR Event: CallTimeout -> $args');
      _ringTimer?.cancel();
      _callTimer?.cancel();
      setState(() {
        _isRinging = false;
        _isIncomingCall = false;
        _isActiveCall = false;
        _activeCallId = null;
        _callStatusText = 'Call Timed Out (15s Ringing)';
      });
      _fetchCallHistory();
    });

    _hubConnection!.on('CalleeUnavailable', (args) {
      _log('SignalR Event: CalleeUnavailable -> $args');
      setState(() {
        _isRinging = false;
        _callStatusText = 'Callee Unavailable';
      });
    });

    _hubConnection!.on('CalleeBusy', (args) {
      _log('SignalR Event: CalleeBusy -> $args');
      setState(() {
        _isRinging = false;
        _callStatusText = 'Callee Busy';
      });
    });

    _hubConnection!.on('MissedCallNotification', (args) {
      _log('SignalR Event: MissedCallNotification -> $args');
      _fetchCallHistory();
    });

    _hubConnection!.on('ConnectRequestReceived', (args) {
      _log('SignalR Event: ConnectRequestReceived -> $args');
      _fetchPendingRequests();
      if (_searchQueryController.text.isNotEmpty) {
        _searchUsers();
      }
    });

    _hubConnection!.on('ConnectRequestAccepted', (args) {
      _log('SignalR Event: ConnectRequestAccepted -> $args');
      _fetchPendingRequests();
      _fetchConnections();
      if (_searchQueryController.text.isNotEmpty) {
        _searchUsers();
      }
    });

    _hubConnection!.on('ConnectRequestDeclined', (args) {
      _log('SignalR Event: ConnectRequestDeclined -> $args');
      _fetchPendingRequests();
      _fetchConnections();
      if (_searchQueryController.text.isNotEmpty) {
        _searchUsers();
      }
    });



    try {
      await _hubConnection!.start();
      setState(() {
        _isHubConnected = true;
      });
      _log('SignalR Connected as User ${_activeSessionIndex} (${session.handle})');
    } catch (e) {
      setState(() {
        _isHubConnected = false;
      });
      _log('SignalR Connection Error: $e');
    }
  }

  void _startRingCountdown() {
    _ringTimer?.cancel();
    _ringTimer = Timer.periodic(const Duration(seconds: 1), (timer) {
      if (_ringTimerSeconds > 1) {
        setState(() {
          _ringTimerSeconds--;
        });
      } else {
        timer.cancel();
        if (_isIncomingCall) {
          setState(() {
            _isIncomingCall = false;
            _callStatusText = 'Missed incoming call (Ring timeout expired)';
          });
        }
      }
    });
  }

  void _startCallTimer() {
    _callTimer?.cancel();
    _callTimer = Timer.periodic(const Duration(seconds: 1), (timer) {
      setState(() {
        _callTimerSeconds++;
      });
    });
  }

  Future<void> _checkHandleAvailability() async {
    final value = _regHandleController.text.trim();
    if (value.isEmpty) {
      setState(() {
        _handleCheckResult = null;
        _isHandleAvailable = null;
      });
      return;
    }

    try {
      final res = await http.get(Uri.parse('$_baseUrl/api/v1/users/check-userid?value=$value'));
      final data = jsonDecode(res.body);
      final bool isAvail = (data['isAvailable'] == true || data['IsAvailable'] == true);
      setState(() {
        _isHandleAvailable = isAvail;
        _handleCheckResult = isAvail ? '✓ Available' : '✗ Already taken';
      });
      _log('Check Handle ($value): ${res.body}');
    } catch (e) {
      setState(() {
        _isHandleAvailable = false;
        _handleCheckResult = 'Could not verify availability';
      });
      _log('Check Handle Exception: $e');
    }
  }

  Future<void> _registerUser(int sessionSlot) async {
    setState(() {
      _authErrorMessage = null;
      _authSuccessMessage = null;
    });

    final handleErr = _validateRegUserId(_regHandleController.text);
    final emailErr = _validateRegEmail(_regEmailController.text);
    final passErr = _validateRegPassword(_regPasswordController.text);
    final confirmPassErr = _validateRegConfirmPassword(_regConfirmPasswordController.text);

    setState(() {
      _regHandleError = handleErr;
      _regEmailError = emailErr;
      _regPasswordError = passErr;
      _regConfirmPasswordError = confirmPassErr;
    });

    if (handleErr != null || emailErr != null || passErr != null || confirmPassErr != null) {
      return;
    }

    setState(() {
      _isAuthLoading = true;
    });

    final body = {
      'userId': _regHandleController.text.trim(),
      'email': _regEmailController.text.trim(),
      'password': _regPasswordController.text.trim(),
      'phoneNumber': _regPhoneController.text.trim(),
    };

    try {
      final res = await http.post(
        Uri.parse('$_baseUrl/api/v1/auth/register'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode(body),
      );

      _log('Register User Slot $sessionSlot -> Status ${res.statusCode}: ${res.body}');

      if (res.statusCode == 200) {
        final data = jsonDecode(res.body);
        final session = UserSession.fromJson(data);
        _saveSessionToLocalStorage(session);
        _scheduleExpiryTimer(session.token);
        setState(() {
          if (sessionSlot == 1) {
            _user1Session = session;
          } else {
            _user2Session = session;
          }
          _activeSessionIndex = sessionSlot;
          _authSuccessMessage = 'Registered and logged in as @${session.handle}!';
        });
        await _connectSignalR();
        _refreshActiveTabData();
      } else {
        String msg = 'Registration failed. Please try again.';
        try {
          final data = jsonDecode(res.body);
          final detail = data['detail']?.toString() ?? data['message']?.toString() ?? data['title']?.toString();
          if (detail != null && detail.isNotEmpty && detail != 'Unauthorized' && detail != 'Bad Request') {
            msg = detail;
          }
        } catch (_) {}
        setState(() {
          _authErrorMessage = msg;
        });
      }
    } catch (e) {
      setState(() {
        _authErrorMessage = 'Connection failed: $e';
      });
      _log('Register Exception: $e');
    } finally {
      setState(() {
        _isAuthLoading = false;
      });
    }
  }

  Future<void> _loginUser(int sessionSlot) async {
    setState(() {
      _authErrorMessage = null;
      _authSuccessMessage = null;
    });

    final emailErr = _validateLoginEmailOrUserId(_loginEmailController.text);
    final passErr = _validateLoginPassword(_loginPasswordController.text);

    setState(() {
      _loginEmailError = emailErr;
      _loginPasswordError = passErr;
    });

    if (emailErr != null || passErr != null) {
      return;
    }

    setState(() {
      _isAuthLoading = true;
    });

    final body = {
      'emailOrUserId': _loginEmailController.text.trim(),
      'password': _loginPasswordController.text.trim(),
    };

    try {
      final res = await http.post(
        Uri.parse('$_baseUrl/api/v1/auth/login'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode(body),
      );

      _log('Login Slot $sessionSlot -> Status ${res.statusCode}: ${res.body}');

      if (res.statusCode == 200) {
        final data = jsonDecode(res.body);
        final session = UserSession.fromJson(data);
        _saveSessionToLocalStorage(session);
        _scheduleExpiryTimer(session.token);
        setState(() {
          if (sessionSlot == 1) {
            _user1Session = session;
          } else {
            _user2Session = session;
          }
          _activeSessionIndex = sessionSlot;
          _authSuccessMessage = 'Logged in successfully as @${session.handle}!';
        });
        await _connectSignalR();
        _refreshActiveTabData();
      } else {
        final input = _loginEmailController.text.trim();
        final isEmail = input.contains('@');
        setState(() {
          _authErrorMessage = isEmail
              ? "Email and password don't match."
              : "User ID and password don't match.";
        });
      }
    } catch (e) {
      setState(() {
        _authErrorMessage = 'Connection failed: $e';
      });
      _log('Login Exception: $e');
    } finally {
      setState(() {
        _isAuthLoading = false;
      });
    }
  }

  Future<void> _searchUsers() async {
    final session = currentSession;
    if (session == null) return;

    final q = _searchQueryController.text.trim();
    if (q.isEmpty) return;

    setState(() {
      _isSearching = true;
    });

    try {
      final res = await http.get(
        Uri.parse('$_baseUrl/api/v1/users/search?query=$q'),
        headers: {'Authorization': 'Bearer ${session.token}'},
      );

      _log('Search Users "$q" -> Status ${res.statusCode}: ${res.body}');
      if (res.statusCode == 401) {
        _handle401();
        return;
      }

      if (res.statusCode == 200) {
        setState(() {
          _searchResults = jsonDecode(res.body);
        });
      } else {
        setState(() {
          _searchResults = [];
        });
      }
    } catch (e) {
      _log('Search Exception: $e');
    } finally {
      setState(() {
        _isSearching = false;
      });
    }
  }

  Future<void> _sendConnectRequest(String targetGuidId, String targetHandle) async {
    final session = currentSession;
    if (session == null) return;

    try {
      final res = await http.post(
        Uri.parse('$_baseUrl/api/v1/connect-requests'),
        headers: {
          'Content-Type': 'application/json',
          'Authorization': 'Bearer ${session.token}',
        },
        body: jsonEncode({'toUserId': targetGuidId}),
      );

      _log('Send Connect Request -> Status ${res.statusCode}: ${res.body}');
      if (res.statusCode == 401) {
        _handle401();
        return;
      }

      if (res.statusCode == 200 || res.statusCode == 201) {
        setState(() {
          final exists = _sentRequests.any((r) => r['toUserId'] == targetGuidId);
          if (!exists) {
            _sentRequests.add({
              'toUserId': targetGuidId,
              'targetHandle': targetHandle,
              'createdAt': DateTime.now().toIso8601String(),
            });
          }
        });
      }

      await _fetchPendingRequests();
      await _fetchConnections();
      if (_searchQueryController.text.trim().isNotEmpty) {
        await _searchUsers();
      }
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(res.statusCode == 200 || res.statusCode == 201 ? 'Connect request sent!' : 'Request sent or pending'),
            behavior: SnackBarBehavior.floating,
          ),
        );
      }
    } catch (e) {
      _log('Send Connect Request Exception: $e');
    }
  }

  Future<void> _fetchPendingRequests() async {
    final session = currentSession;
    if (session == null) return;

    try {
      final res = await http.get(
        Uri.parse('$_baseUrl/api/v1/connect-requests/pending'),
        headers: {'Authorization': 'Bearer ${session.token}'},
      );

      if (res.statusCode == 401) {
        _handle401();
        return;
      }

      if (res.statusCode == 200) {
        setState(() {
          _pendingRequests = jsonDecode(res.body);
        });
      }
    } catch (e) {
      _log('Fetch Pending Requests Exception: $e');
    }
  }

  Future<void> _acceptConnectRequest(String requestId) async {
    final session = currentSession;
    if (session == null || requestId.isEmpty || requestId == 'null') return;

    try {
      final res = await http.post(
        Uri.parse('$_baseUrl/api/v1/connect-requests/$requestId/accept'),
        headers: {'Authorization': 'Bearer ${session.token}'},
      );

      _log('Accept Request $requestId -> Status ${res.statusCode}: ${res.body}');
      if (res.statusCode == 401) {
        _handle401();
        return;
      }

      await _fetchPendingRequests();
      await _fetchConnections();
      if (_searchQueryController.text.trim().isNotEmpty) {
        await _searchUsers();
      }
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Connection accepted!'), behavior: SnackBarBehavior.floating),
        );
      }
    } catch (e) {
      _log('Accept Request Exception: $e');
    }
  }

  Future<void> _declineConnectRequest(String requestId) async {
    final session = currentSession;
    if (session == null || requestId.isEmpty || requestId == 'null') return;

    try {
      final res = await http.post(
        Uri.parse('$_baseUrl/api/v1/connect-requests/$requestId/decline'),
        headers: {'Authorization': 'Bearer ${session.token}'},
      );

      _log('Decline Request $requestId -> Status ${res.statusCode}: ${res.body}');
      if (res.statusCode == 401) {
        _handle401();
        return;
      }

      await _fetchPendingRequests();
      await _fetchConnections();
      if (_searchQueryController.text.trim().isNotEmpty) {
        await _searchUsers();
      }
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Connect request declined.'), behavior: SnackBarBehavior.floating),
        );
      }
    } catch (e) {
      _log('Decline Request Exception: $e');
    }
  }


  Future<void> _fetchConnections() async {
    final session = currentSession;
    if (session == null) return;

    try {
      final res = await http.get(
        Uri.parse('$_baseUrl/api/v1/connections'),
        headers: {'Authorization': 'Bearer ${session.token}'},
      );

      if (res.statusCode == 401) {
        _handle401();
        return;
      }

      if (res.statusCode == 200) {
        setState(() {
          _connections = jsonDecode(res.body);
          _sentRequests.removeWhere((req) => _connections.any((c) =>
              (c['contactId'] ?? c['connectedUserId']) == req['toUserId'] ||
              (c['contactUserId'] ?? c['userId']) == req['targetHandle']));
        });
      }
    } catch (e) {
      _log('Fetch Connections Exception: $e');
    }
  }

  Future<void> _initiateCall(String targetGuidId, String targetHandle) async {
    if (_hubConnection == null) return;

    setState(() {
      _isRinging = true;
      _callerOrCalleeName = targetHandle;
      _callStatusText = 'Initiating call to $targetHandle...';
      _ringTimerSeconds = 15;
    });

    _log('Initiating call via SignalR to $targetGuidId ($targetHandle)');
    try {
      await _hubConnection!.invoke('InitiateCallAttempt', args: [targetGuidId]);
    } catch (e) {
      _log('InitiateCallAttempt Error: $e');
      setState(() {
        _isRinging = false;
        _callStatusText = 'Call Failed: $e';
      });
    }
  }

  Future<void> _respondToCall(bool accepted) async {
    if (_hubConnection == null || _activeCallId == null) return;

    _ringTimer?.cancel();
    _log('Responding to call $_activeCallId: accepted=$accepted');
    try {
      await _hubConnection!.invoke('RespondToCall', args: <Object>[_activeCallId!, accepted]);
      if (accepted) {
        setState(() {
          _isIncomingCall = false;
          _isActiveCall = true;
          _callStatusText = 'Call Active';
          _callTimerSeconds = 0;
        });
        _startCallTimer();
      } else {
        setState(() {
          _isIncomingCall = false;
          _activeCallId = null;
          _callStatusText = 'Call Rejected';
        });
      }
    } catch (e) {
      _log('RespondToCall Error: $e');
    }
  }

  Future<void> _endCall() async {
    if (_activeCallId == null) return;

    _callTimer?.cancel();
    _ringTimer?.cancel();
    _log('Ending call $_activeCallId');

    final session = currentSession;
    if (session != null) {
      try {
        final res = await http.post(
          Uri.parse('$_baseUrl/api/v1/calls/$_activeCallId/end'),
          headers: {'Authorization': 'Bearer ${session.token}'},
        );
        _log('REST EndCall -> Status ${res.statusCode}: ${res.body}');
        if (res.statusCode == 401) {
          _handle401();
          return;
        }
      } catch (e) {
        _log('REST EndCall Exception: $e');
      }
    }

    if (_hubConnection != null) {
      try {
        await _hubConnection!.invoke('EndCall', args: <Object>[_activeCallId!]);
      } catch (e) {
        _log('SignalR EndCall Exception: $e');
      }
    }

    setState(() {
      _isActiveCall = false;
      _isRinging = false;
      _isIncomingCall = false;
      _activeCallId = null;
      _callStatusText = 'Call Ended';
    });

    _fetchCallHistory();
  }

  Future<void> _fetchCallHistory() async {
    final session = currentSession;
    if (session == null) return;

    try {
      final res = await http.get(
        Uri.parse('$_baseUrl/api/v1/calls/history?pageNumber=1&pageSize=20'),
        headers: {'Authorization': 'Bearer ${session.token}'},
      );

      if (res.statusCode == 401) {
        _handle401();
        return;
      }

      if (res.statusCode == 200) {
        final data = jsonDecode(res.body);
        setState(() {
          _callHistory = data['items'] ?? [];
        });
      }
    } catch (e) {
      _log('Fetch Call History Exception: $e');
    }
  }

  Future<void> _blockUser(String targetGuidId) async {
    final session = currentSession;
    if (session == null) return;

    try {
      final res = await http.post(
        Uri.parse('$_baseUrl/api/v1/users/$targetGuidId/block'),
        headers: {'Authorization': 'Bearer ${session.token}'},
      );

      _log('Block User $targetGuidId -> Status ${res.statusCode}');
      if (res.statusCode == 401) {
        _handle401();
        return;
      }

      await _fetchBlockedUsers();
      await _fetchConnections();
      await _fetchPendingRequests();
      if (_searchQueryController.text.trim().isNotEmpty) {
        await _searchUsers();
      }
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('User blocked'), behavior: SnackBarBehavior.floating),
        );
      }
    } catch (e) {
      _log('Block User Exception: $e');
    }
  }

  Future<void> _unblockUser(String targetGuidId) async {
    final session = currentSession;
    if (session == null) return;

    try {
      final res = await http.delete(
        Uri.parse('$_baseUrl/api/v1/users/$targetGuidId/block'),
        headers: {'Authorization': 'Bearer ${session.token}'},
      );

      _log('Unblock User $targetGuidId -> Status ${res.statusCode}');
      if (res.statusCode == 401) {
        _handle401();
        return;
      }

      await _fetchBlockedUsers();
      await _fetchConnections();
      await _fetchPendingRequests();
      if (_searchQueryController.text.trim().isNotEmpty) {
        await _searchUsers();
      }
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('User unblocked'), behavior: SnackBarBehavior.floating),
        );
      }
    } catch (e) {
      _log('Unblock User Exception: $e');
    }
  }

  Future<void> _fetchBlockedUsers() async {
    final session = currentSession;
    if (session == null) return;

    try {
      final res = await http.get(
        Uri.parse('$_baseUrl/api/v1/users/blocked'),
        headers: {'Authorization': 'Bearer ${session.token}'},
      );

      if (res.statusCode == 401) {
        _handle401();
        return;
      }

      if (res.statusCode == 200) {
        setState(() {
          _blockedUsers = jsonDecode(res.body);
        });
      }
    } catch (e) {
      _log('Fetch Blocked Users Exception: $e');
    }
  }

  Future<void> _reportUser(String reportedGuidId) async {
    final session = currentSession;
    if (session == null) return;

    final body = {
      'reportedUserId': reportedGuidId,
      'reason': _reportReasonController.text.trim().isEmpty ? 'Spam' : _reportReasonController.text.trim(),
      'note': _reportNoteController.text.trim(),
    };

    try {
      final res = await http.post(
        Uri.parse('$_baseUrl/api/v1/reports'),
        headers: {
          'Content-Type': 'application/json',
          'Authorization': 'Bearer ${session.token}',
        },
        body: jsonEncode(body),
      );

      _log('Report User $reportedGuidId -> Status ${res.statusCode}: ${res.body}');
      if (res.statusCode == 401) {
        _handle401();
        return;
      }

      _reportReasonController.clear();
      _reportNoteController.clear();
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Report submitted. Thank you.'), behavior: SnackBarBehavior.floating),
        );
      }
    } catch (e) {
      _log('Report User Exception: $e');
    }
  }

  Future<void> _softDeleteAccount() async {
    final session = currentSession;
    if (session == null) return;

    try {
      final res = await http.delete(
        Uri.parse('$_baseUrl/api/v1/account'),
        headers: {'Authorization': 'Bearer ${session.token}'},
      );

      _log('Soft Delete Account -> Status ${res.statusCode}');
      if (res.statusCode == 401) {
        _handle401();
        return;
      }

      if (res.statusCode == 204 || res.statusCode == 200) {
        _log('Account soft-deleted. Log in again within 60 days to reactivate.');
        _logout();
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(
              content: Text('Account marked for deletion (60-day recovery window). Logged out.'),
              behavior: SnackBarBehavior.floating,
            ),
          );
        }
      }
    } catch (e) {
      _log('Soft Delete Exception: $e');
    }
  }

  void _refreshActiveTabData() {
    _fetchPendingRequests();
    _fetchConnections();
    _fetchCallHistory();
    _fetchBlockedUsers();
  }

  void _switchSession(int slot) async {
    setState(() {
      _activeSessionIndex = slot;
    });
    _log('Switched Active Session Slot to User $slot (${currentSession?.handle ?? 'Logged Out'})');
    if (currentSession != null) {
      _scheduleExpiryTimer(currentSession!.token);
      await _connectSignalR();
      _refreshActiveTabData();
    } else {
      _expiryTimer?.cancel();
      _expiryTimer = null;
      _hubConnection?.stop();
      setState(() {
        _isHubConnected = false;
      });
    }
  }

  void _showProfilePanelModal() {
    final session = currentSession;
    if (session == null) return;

    showDialog(
      context: context,
      builder: (ctx) => Dialog(
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(24)),
        child: Container(
          width: 360,
          padding: const EdgeInsets.all(28),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              CircleAvatar(
                radius: 44,
                backgroundColor: const Color(0xFF0D9488),
                child: Text(
                  session.handle.isNotEmpty ? session.handle[0].toUpperCase() : 'U',
                  style: const TextStyle(fontSize: 36, fontWeight: FontWeight.bold, color: Colors.white),
                ),
              ),
              const SizedBox(height: 16),
              Text(
                '@${session.handle}',
                style: const TextStyle(fontSize: 22, fontWeight: FontWeight.bold, color: Color(0xFF0F172A)),
              ),
              const SizedBox(height: 4),
              Text(
                session.email.isNotEmpty ? session.email : 'No email address',
                style: const TextStyle(fontSize: 14, color: Color(0xFF64748B)),
              ),
              const SizedBox(height: 24),
              SizedBox(
                width: double.infinity,
                child: OutlinedButton(
                  onPressed: () => Navigator.of(ctx).pop(),
                  child: const Text('Close'),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  void _showReportUserDialog() {
    showDialog(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Report a User', style: TextStyle(fontWeight: FontWeight.bold)),
        content: SizedBox(
          width: 400,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Text('Select a contact to report and provide details below.', style: TextStyle(color: Color(0xFF64748B), fontSize: 13)),
              const SizedBox(height: 16),
              if (_connections.isNotEmpty)
                DropdownButtonFormField<String>(
                  decoration: const InputDecoration(labelText: 'Select Contact'),
                  items: _connections.map<DropdownMenuItem<String>>((c) {
                    final cId = c['contactId'] ?? c['connectedUserId'];
                    final cHandle = c['contactUserId'] ?? c['userId'] ?? cId;
                    return DropdownMenuItem<String>(
                      value: cId,
                      child: Text('@$cHandle'),
                    );
                  }).toList(),
                  onChanged: (val) {
                    if (val != null) {
                      _reportUser(val);
                    }
                  },
                )
              else
                const Text('No connections available to report.', style: TextStyle(color: Color(0xFF94A3B8), fontSize: 12)),
              const SizedBox(height: 12),
              TextField(
                controller: _reportReasonController,
                decoration: const InputDecoration(labelText: 'Reason (e.g. Spam, Harassment)'),
              ),
              const SizedBox(height: 12),
              TextField(
                controller: _reportNoteController,
                decoration: const InputDecoration(labelText: 'Details / Note'),
              ),
            ],
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(ctx).pop(),
            child: const Text('Cancel'),
          ),
          ElevatedButton(
            onPressed: () {
              Navigator.of(ctx).pop();
              if (_connections.isNotEmpty) {
                ScaffoldMessenger.of(context).showSnackBar(
                  const SnackBar(content: Text('Report submitted cleanly.'), behavior: SnackBarBehavior.floating),
                );
              }
            },
            child: const Text('Submit Report'),
          ),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final session = currentSession;
    final isWide = MediaQuery.of(context).size.width > 768;

    if (session == null) {
      return Scaffold(
        body: SafeArea(
          child: Stack(
            children: [
              Center(child: SingleChildScrollView(child: _buildAuthCard())),
              Positioned(
                bottom: 16,
                right: 16,
                child: FloatingActionButton.small(
                  onPressed: () => setState(() => _showDevConsole = !_showDevConsole),
                  backgroundColor: const Color(0xFF334155),
                  child: const Icon(Icons.developer_mode, color: Colors.white),
                ),
              ),
              if (_showDevConsole) _buildDevConsoleSheet(),
            ],
          ),
        ),
      );
    }

    return Scaffold(
      appBar: AppBar(
        title: InkWell(
          borderRadius: BorderRadius.circular(12),
          onTap: _showProfilePanelModal,
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 4),
            child: Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                Container(
                  width: 36,
                  height: 36,
                  decoration: const BoxDecoration(
                    color: Color(0xFF0D9488),
                    shape: BoxShape.circle,
                  ),
                  alignment: Alignment.center,
                  child: Text(
                    session.handle.isNotEmpty ? session.handle[0].toUpperCase() : 'U',
                    style: const TextStyle(color: Colors.white, fontWeight: FontWeight.bold, fontSize: 16),
                  ),
                ),
                const SizedBox(width: 12),
                Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(
                      '@${session.handle}',
                      style: const TextStyle(fontSize: 16, fontWeight: FontWeight.bold),
                    ),
                    Row(
                      children: [
                        Container(
                          width: 8,
                          height: 8,
                          decoration: BoxDecoration(
                            color: _isHubConnected ? const Color(0xFF10B981) : const Color(0xFFEF4444),
                            shape: BoxShape.circle,
                          ),
                        ),
                        const SizedBox(width: 6),
                        Text(
                          _isHubConnected ? 'Connected' : 'Offline',
                          style: const TextStyle(fontSize: 12, color: Color(0xFF64748B)),
                        ),
                      ],
                    ),
                  ],
                ),
              ],
            ),
          ),
        ),
        actions: [
          IconButton(
            icon: Icon(
              _showDevConsole ? Icons.developer_mode : Icons.bug_report_outlined,
              size: 18,
              color: const Color(0xFF94A3B8),
            ),
            tooltip: 'Developer Tools',
            onPressed: () => setState(() => _showDevConsole = !_showDevConsole),
          ),
          IconButton(
            icon: const Icon(Icons.logout, color: Color(0xFF64748B)),
            tooltip: 'Log Out',
            onPressed: _logout,
          ),
          const SizedBox(width: 8),
        ],
      ),
      body: Stack(
        children: [
          Row(
            children: [
              if (isWide)
                NavigationRail(
                  selectedIndex: _selectedNavIndex,
                  onDestinationSelected: (idx) => setState(() => _selectedNavIndex = idx),
                  labelType: NavigationRailLabelType.all,
                  selectedIconTheme: const IconThemeData(color: Color(0xFF0D9488)),
                  selectedLabelTextStyle: const TextStyle(color: Color(0xFF0D9488), fontWeight: FontWeight.bold),
                  destinations: [
                    const NavigationRailDestination(
                      icon: Icon(Icons.people_outline),
                      selectedIcon: Icon(Icons.people),
                      label: Text('Contacts'),
                    ),
                    NavigationRailDestination(
                      icon: Badge(
                        isLabelVisible: _pendingRequests.isNotEmpty,
                        label: Text('${_pendingRequests.length}'),
                        child: const Icon(Icons.inbox_outlined),
                      ),
                      selectedIcon: Badge(
                        isLabelVisible: _pendingRequests.isNotEmpty,
                        label: Text('${_pendingRequests.length}'),
                        child: const Icon(Icons.inbox),
                      ),
                      label: const Text('Requests'),
                    ),
                    const NavigationRailDestination(
                      icon: Icon(Icons.phone_outlined),
                      selectedIcon: Icon(Icons.phone),
                      label: Text('Calls'),
                    ),
                    const NavigationRailDestination(
                      icon: Icon(Icons.history_outlined),
                      selectedIcon: Icon(Icons.history),
                      label: Text('History'),
                    ),
                    const NavigationRailDestination(
                      icon: Icon(Icons.settings_outlined),
                      selectedIcon: Icon(Icons.settings),
                      label: Text('Settings'),
                    ),
                  ],
                ),
              Expanded(
                child: Container(
                  color: const Color(0xFFF8FAFC),
                  child: Column(
                    children: [
                      if (_isIncomingCall || _isRinging || _isActiveCall || _activeCallId != null)
                        _buildCallTopBanner(),
                      Expanded(
                        child: IndexedStack(
                          index: _selectedNavIndex,
                          children: [
                            _buildContactsScreen(),
                            _buildConnectRequestsScreen(),
                            _buildCallingScreen(),
                            _buildCallHistoryScreen(),
                            _buildAccountSettingsScreen(),
                          ],
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            ],
          ),
          if (_isIncomingCall || _isActiveCall)
            Positioned.fill(child: _buildFullCallOverlay()),
          if (_showDevConsole) _buildDevConsoleSheet(),
        ],
      ),
      bottomNavigationBar: isWide
          ? null
          : NavigationBar(
              selectedIndex: _selectedNavIndex,
              onDestinationSelected: (idx) => setState(() => _selectedNavIndex = idx),
              indicatorColor: const Color(0xFFCCFBF1),
              destinations: [
                const NavigationDestination(
                  icon: Icon(Icons.people_outline),
                  selectedIcon: Icon(Icons.people, color: Color(0xFF0D9488)),
                  label: 'Contacts',
                ),
                NavigationDestination(
                  icon: Badge(
                    isLabelVisible: _pendingRequests.isNotEmpty,
                    label: Text('${_pendingRequests.length}'),
                    child: const Icon(Icons.inbox_outlined),
                  ),
                  selectedIcon: Badge(
                    isLabelVisible: _pendingRequests.isNotEmpty,
                    label: Text('${_pendingRequests.length}'),
                    child: const Icon(Icons.inbox, color: Color(0xFF0D9488)),
                  ),
                  label: 'Requests',
                ),
                const NavigationDestination(
                  icon: Icon(Icons.phone_outlined),
                  selectedIcon: Icon(Icons.phone, color: Color(0xFF0D9488)),
                  label: 'Calling',
                ),
                const NavigationDestination(
                  icon: Icon(Icons.history_outlined),
                  selectedIcon: Icon(Icons.history, color: Color(0xFF0D9488)),
                  label: 'History',
                ),
                const NavigationDestination(
                  icon: Icon(Icons.settings_outlined),
                  selectedIcon: Icon(Icons.settings, color: Color(0xFF0D9488)),
                  label: 'Settings',
                ),
              ],
            ),
    );
  }

  Widget _buildAuthCard() {
    return Container(
      width: 420,
      padding: const EdgeInsets.all(28),
      margin: const EdgeInsets.symmetric(horizontal: 16, vertical: 24),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(24),
        boxShadow: const [
          BoxShadow(
            color: Color(0x0F0F172A),
            blurRadius: 24,
            offset: Offset(0, 8),
          ),
        ],
        border: Border.all(color: const Color(0xFFE2E8F0)),
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Container(
                width: 44,
                height: 44,
                decoration: const BoxDecoration(
                  color: Color(0x1F0D9488),
                  shape: BoxShape.circle,
                ),
                child: const Icon(Icons.forum_rounded, color: Color(0xFF0D9488), size: 26),
              ),
              const SizedBox(width: 12),
              const Text(
                'Connect',
                style: TextStyle(
                  fontSize: 26,
                  fontWeight: FontWeight.bold,
                  color: Color(0xFF0F172A),
                  letterSpacing: -0.5,
                ),
              ),
            ],
          ),
          const SizedBox(height: 8),
          const Text(
            'Simple, secure voice calls and connections',
            textAlign: TextAlign.center,
            style: TextStyle(color: Color(0xFF64748B), fontSize: 13),
          ),
          const SizedBox(height: 24),
          Container(
            decoration: BoxDecoration(
              color: const Color(0xFFF1F5F9),
              borderRadius: BorderRadius.circular(12),
            ),
            padding: const EdgeInsets.all(4),
            child: Row(
              children: [
                Expanded(
                  child: GestureDetector(
                    onTap: () => setState(() {
                      _isAuthModeLogin = true;
                      _clearAuthErrorsAndFields();
                    }),
                    child: Container(
                      padding: const EdgeInsets.symmetric(vertical: 10),
                      decoration: BoxDecoration(
                        color: _isAuthModeLogin ? Colors.white : Colors.transparent,
                        borderRadius: BorderRadius.circular(10),
                        boxShadow: _isAuthModeLogin
                            ? [const BoxShadow(color: Color(0x0A000000), blurRadius: 4, offset: Offset(0, 2))]
                            : [],
                      ),
                      alignment: Alignment.center,
                      child: Text(
                        'Log In',
                        style: TextStyle(
                          fontWeight: FontWeight.w600,
                          color: _isAuthModeLogin ? const Color(0xFF0F172A) : const Color(0xFF64748B),
                        ),
                      ),
                    ),
                  ),
                ),
                Expanded(
                  child: GestureDetector(
                    onTap: () => setState(() {
                      _isAuthModeLogin = false;
                      _clearAuthErrorsAndFields();
                    }),
                    child: Container(
                      padding: const EdgeInsets.symmetric(vertical: 10),
                      decoration: BoxDecoration(
                        color: !_isAuthModeLogin ? Colors.white : Colors.transparent,
                        borderRadius: BorderRadius.circular(10),
                        boxShadow: !_isAuthModeLogin
                            ? [const BoxShadow(color: Color(0x0A000000), blurRadius: 4, offset: Offset(0, 2))]
                            : [],
                      ),
                      alignment: Alignment.center,
                      child: Text(
                        'Create Account',
                        style: TextStyle(
                          fontWeight: FontWeight.w600,
                          color: !_isAuthModeLogin ? const Color(0xFF0F172A) : const Color(0xFF64748B),
                        ),
                      ),
                    ),
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: 20),
          if (_authErrorMessage != null)
            Container(
              padding: const EdgeInsets.all(12),
              margin: const EdgeInsets.only(bottom: 16),
              decoration: BoxDecoration(
                color: const Color(0xFFFFF1F2),
                borderRadius: BorderRadius.circular(10),
                border: Border.all(color: const Color(0xFFFECDD3)),
              ),
              child: Row(
                children: [
                  const Icon(Icons.error_outline, color: Color(0xFFE11D48), size: 20),
                  const SizedBox(width: 10),
                  Expanded(
                    child: Text(
                      _authErrorMessage!,
                      style: const TextStyle(color: Color(0xFFBE123C), fontSize: 13),
                    ),
                  ),
                ],
              ),
            ),
          if (_authSuccessMessage != null)
            Container(
              padding: const EdgeInsets.all(12),
              margin: const EdgeInsets.only(bottom: 16),
              decoration: BoxDecoration(
                color: const Color(0xFFECFDF5),
                borderRadius: BorderRadius.circular(10),
                border: Border.all(color: const Color(0xFFA7F3D0)),
              ),
              child: Row(
                children: [
                  const Icon(Icons.check_circle_outline, color: Color(0xFF059669), size: 20),
                  const SizedBox(width: 10),
                  Expanded(
                    child: Text(
                      _authSuccessMessage!,
                      style: const TextStyle(color: Color(0xFF047857), fontSize: 13),
                    ),
                  ),
                ],
              ),
            ),
          if (_isAuthModeLogin) ...[
            TextField(
              controller: _loginEmailController,
              focusNode: _loginEmailFocusNode,
              decoration: InputDecoration(
                labelText: 'Email or User ID',
                prefixIcon: const Icon(Icons.person_outline, size: 20),
                errorText: _loginEmailError,
              ),
            ),
            const SizedBox(height: 14),
            TextField(
              controller: _loginPasswordController,
              focusNode: _loginPasswordFocusNode,
              obscureText: _obscureLoginPassword,
              decoration: InputDecoration(
                labelText: 'Password',
                prefixIcon: const Icon(Icons.lock_outline, size: 20),
                suffixIcon: IconButton(
                  icon: Icon(
                    _obscureLoginPassword ? Icons.visibility_off_outlined : Icons.visibility_outlined,
                    size: 20,
                  ),
                  onPressed: () {
                    setState(() {
                      _obscureLoginPassword = !_obscureLoginPassword;
                    });
                  },
                ),
                errorText: _loginPasswordError,
              ),
            ),
            const SizedBox(height: 20),
            ElevatedButton(
              onPressed: _isAuthLoading ? null : () => _loginUser(_activeSessionIndex),
              child: _isAuthLoading
                  ? const SizedBox(width: 20, height: 20, child: CircularProgressIndicator(color: Colors.white, strokeWidth: 2))
                  : const Text('Log In'),
            ),
          ] else ...[
            TextField(
              controller: _regHandleController,
              focusNode: _regHandleFocusNode,
              decoration: InputDecoration(
                labelText: 'User ID',
                prefixIcon: const Icon(Icons.person_outline, size: 20),
                errorText: _regHandleError,
              ),
            ),
            if (_handleCheckResult != null)
              Padding(
                padding: const EdgeInsets.only(top: 6, left: 4),
                child: Text(
                  _handleCheckResult!,
                  style: TextStyle(
                    fontSize: 12,
                    fontWeight: FontWeight.w500,
                    color: _isHandleAvailable == true ? const Color(0xFF059669) : const Color(0xFFE11D48),
                  ),
                ),
              ),
            const SizedBox(height: 12),
            TextField(
              controller: _regEmailController,
              focusNode: _regEmailFocusNode,
              decoration: InputDecoration(
                labelText: 'Email Address',
                prefixIcon: const Icon(Icons.email_outlined, size: 20),
                errorText: _regEmailError,
              ),
            ),
            const SizedBox(height: 12),
            TextField(
              controller: _regPasswordController,
              focusNode: _regPasswordFocusNode,
              obscureText: _obscureRegPassword,
              decoration: InputDecoration(
                labelText: 'Password',
                prefixIcon: const Icon(Icons.lock_outline, size: 20),
                suffixIcon: IconButton(
                  icon: Icon(
                    _obscureRegPassword ? Icons.visibility_off_outlined : Icons.visibility_outlined,
                    size: 20,
                  ),
                  onPressed: () {
                    setState(() {
                      _obscureRegPassword = !_obscureRegPassword;
                    });
                  },
                ),
                errorText: _regPasswordError,
              ),
            ),
            const SizedBox(height: 12),
            TextField(
              controller: _regConfirmPasswordController,
              focusNode: _regConfirmPasswordFocusNode,
              obscureText: _obscureRegConfirmPassword,
              decoration: InputDecoration(
                labelText: 'Confirm Password',
                prefixIcon: const Icon(Icons.lock_outline, size: 20),
                suffixIcon: IconButton(
                  icon: Icon(
                    _obscureRegConfirmPassword ? Icons.visibility_off_outlined : Icons.visibility_outlined,
                    size: 20,
                  ),
                  onPressed: () {
                    setState(() {
                      _obscureRegConfirmPassword = !_obscureRegConfirmPassword;
                    });
                  },
                ),
                errorText: _regConfirmPasswordError,
              ),
            ),
            const SizedBox(height: 12),
            TextField(
              controller: _regPhoneController,
              decoration: const InputDecoration(
                labelText: 'Phone Number (Optional)',
                prefixIcon: Icon(Icons.phone_outlined, size: 20),
              ),
            ),
            const SizedBox(height: 20),
            ElevatedButton(
              onPressed: _isAuthLoading ? null : () => _registerUser(_activeSessionIndex),
              child: _isAuthLoading
                  ? const SizedBox(width: 20, height: 20, child: CircularProgressIndicator(color: Colors.white, strokeWidth: 2))
                  : const Text('Create Account'),
            ),
          ],
        ],
      ),
    );
  }

  Widget _buildContactsScreen() {
    return SingleChildScrollView(
      padding: const EdgeInsets.all(20),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text('Contacts', style: TextStyle(fontSize: 22, fontWeight: FontWeight.bold, color: Color(0xFF0F172A))),
          const SizedBox(height: 4),
          const Text('Search and connect with friends or view existing connections', style: TextStyle(color: Color(0xFF64748B), fontSize: 13)),
          const SizedBox(height: 16),
          Row(
            children: [
              Expanded(
                child: TextField(
                  controller: _searchQueryController,
                  onSubmitted: (_) => _searchUsers(),
                  decoration: const InputDecoration(
                    hintText: 'Search user handle (e.g. user_two) or phone...',
                    prefixIcon: Icon(Icons.search, size: 22),
                  ),
                ),
              ),
              const SizedBox(width: 12),
              ElevatedButton(
                onPressed: _searchUsers,
                child: const Text('Search'),
              ),
            ],
          ),
          const SizedBox(height: 24),
          if (_isSearching)
            const Center(child: Padding(padding: EdgeInsets.all(24), child: CircularProgressIndicator()))
          else if (_searchResults.isNotEmpty) ...[
            const Text('Search Results', style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold, color: Color(0xFF334155))),
            const SizedBox(height: 12),
            ListView.separated(
              shrinkWrap: true,
              physics: const NeverScrollableScrollPhysics(),
              itemCount: _searchResults.length,
              separatorBuilder: (_, __) => const SizedBox(height: 10),
              itemBuilder: (context, index) {
                final item = _searchResults[index];
                final guidId = item['id'];
                final handle = item['userId'] ?? 'User';
                final phone = item['phoneNumber'] ?? 'N/A';

                return Card(
                  child: Padding(
                    padding: const EdgeInsets.all(12),
                    child: Row(
                      children: [
                        CircleAvatar(
                          backgroundColor: const Color(0xFFCCFBF1),
                          foregroundColor: const Color(0xFF0D9488),
                          child: Text(handle.isNotEmpty ? handle[0].toUpperCase() : 'U'),
                        ),
                        const SizedBox(width: 12),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text('@$handle', style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 15)),
                              Text('Phone: $phone', style: const TextStyle(color: Color(0xFF64748B), fontSize: 12)),
                            ],
                          ),
                        ),
                        Builder(
                          builder: (context) {
                            final bool isConnected = item['isConnected'] == true;
                            final bool hasPendingRequest = item['hasPendingRequest'] == true;
                            final String? pendingRequestId = item['pendingRequestId']?.toString();

                             dynamic receivedReq;
                            for (final r in _pendingRequests) {
                              final rId = (r['id'] ?? r['requestId'])?.toString();
                              final sId = (r['fromUserHandle'] ?? r['senderUserId'])?.toString();
                              final fId = r['fromUserId']?.toString();
                              if ((pendingRequestId != null && rId == pendingRequestId) ||
                                  sId == handle || fId == guidId) {
                                receivedReq = r;
                                break;
                              }
                            }

                            final String? reqIdToUse = pendingRequestId ?? (receivedReq?['id'] ?? receivedReq?['requestId'])?.toString();

                            if (isConnected) {
                              return Container(
                                padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
                                decoration: BoxDecoration(
                                  color: const Color(0xFFECFDF5),
                                  borderRadius: BorderRadius.circular(10),
                                  border: Border.all(color: const Color(0xFFA7F3D0)),
                                ),
                                child: Row(
                                  mainAxisSize: MainAxisSize.min,
                                  children: const [
                                    Icon(Icons.check_circle_outline, size: 18, color: Color(0xFF059669)),
                                    SizedBox(width: 6),
                                    Text(
                                      'Connected',
                                      style: TextStyle(fontSize: 13, fontWeight: FontWeight.bold, color: Color(0xFF047857)),
                                    ),
                                  ],
                                ),
                              );
                            } else if (hasPendingRequest && receivedReq != null) {
                              return Row(
                                mainAxisSize: MainAxisSize.min,
                                children: [
                                  ElevatedButton(
                                    onPressed: reqIdToUse == null ? null : () => _acceptConnectRequest(reqIdToUse),
                                    style: ElevatedButton.styleFrom(
                                      backgroundColor: const Color(0xFF0D9488),
                                      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
                                    ),
                                    child: const Text('Accept', style: TextStyle(fontSize: 13)),
                                  ),
                                  const SizedBox(width: 8),
                                  OutlinedButton(
                                    onPressed: reqIdToUse == null ? null : () => _declineConnectRequest(reqIdToUse),
                                    style: OutlinedButton.styleFrom(
                                      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
                                      foregroundColor: const Color(0xFFE11D48),
                                      side: const BorderSide(color: Color(0xFFFECDD3)),
                                    ),
                                    child: const Text('Decline', style: TextStyle(fontSize: 13)),
                                  ),
                                ],
                              );
                            } else if (hasPendingRequest || _sentRequests.any((r) => r['toUserId'] == guidId)) {
                              return Container(
                                padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
                                decoration: BoxDecoration(
                                  color: const Color(0xFFF1F5F9),
                                  borderRadius: BorderRadius.circular(10),
                                  border: Border.all(color: const Color(0xFFCBD5E1)),
                                ),
                                child: Row(
                                  mainAxisSize: MainAxisSize.min,
                                  children: const [
                                    Icon(Icons.outbox_outlined, size: 16, color: Color(0xFF64748B)),
                                    SizedBox(width: 6),
                                    Text(
                                      'Request Sent',
                                      style: TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: Color(0xFF64748B)),
                                    ),
                                  ],
                                ),
                              );
                            } else {
                              return ElevatedButton.icon(
                                onPressed: () => _sendConnectRequest(guidId, handle),
                                icon: const Icon(Icons.person_add_alt_1, size: 18),
                                label: const Text('Send Request'),
                                style: ElevatedButton.styleFrom(
                                  padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
                                  textStyle: const TextStyle(fontSize: 13),
                                ),
                              );
                            }
                          },
                        ),
                      ],
                    ),
                  ),
                );
              },
            ),
            const SizedBox(height: 24),
            const Divider(),
            const SizedBox(height: 16),
          ],
          const Text('Connected Contacts', style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold, color: Color(0xFF0F172A))),
          const SizedBox(height: 12),
          if (_connections.isEmpty)
            Container(
              padding: const EdgeInsets.all(32),
              decoration: BoxDecoration(
                color: Colors.white,
                borderRadius: BorderRadius.circular(16),
                border: Border.all(color: const Color(0xFFE2E8F0)),
              ),
              alignment: Alignment.center,
              child: Column(
                children: const [
                  Icon(Icons.people_outline, size: 48, color: Color(0xFFCBD5E1)),
                  SizedBox(height: 12),
                  Text('No connected contacts yet', style: TextStyle(fontWeight: FontWeight.w600, color: Color(0xFF475569))),
                  SizedBox(height: 4),
                  Text('Search for users above and send a connect request.', style: TextStyle(color: Color(0xFF94A3B8), fontSize: 12)),
                ],
              ),
            )
          else
            ListView.separated(
              shrinkWrap: true,
              physics: const NeverScrollableScrollPhysics(),
              itemCount: _connections.length,
              separatorBuilder: (_, __) => const SizedBox(height: 10),
              itemBuilder: (context, index) {
                final conn = _connections[index];
                final targetGuidId = conn['contactId'] ?? conn['connectedUserId'];
                final handle = conn['contactUserId'] ?? conn['userId'] ?? 'Contact';
                final presence = conn['presenceStatus'] ?? 'Offline';
                final isOnline = presence.toString().toLowerCase() == 'online';

                return Card(
                  child: ListTile(
                    contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
                    leading: Stack(
                      children: [
                        CircleAvatar(
                          backgroundColor: const Color(0xFFF1F5F9),
                          foregroundColor: const Color(0xFF334155),
                          child: Text(handle.isNotEmpty ? handle[0].toUpperCase() : 'C'),
                        ),
                        Positioned(
                          right: 0,
                          bottom: 0,
                          child: Container(
                            width: 12,
                            height: 12,
                            decoration: BoxDecoration(
                              color: isOnline ? const Color(0xFF10B981) : const Color(0xFFCBD5E1),
                              shape: BoxShape.circle,
                              border: Border.all(color: Colors.white, width: 2),
                            ),
                          ),
                        ),
                      ],
                    ),
                    title: Text('@$handle', style: const TextStyle(fontWeight: FontWeight.bold)),
                    subtitle: Text(
                      'Status: $presence',
                      style: TextStyle(color: isOnline ? const Color(0xFF059669) : const Color(0xFF64748B), fontSize: 12),
                    ),
                    trailing: ElevatedButton.icon(
                      onPressed: () => _initiateCall(targetGuidId, handle),
                      icon: const Icon(Icons.phone, size: 16),
                      label: const Text('Call'),
                      style: ElevatedButton.styleFrom(
                        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
                        textStyle: const TextStyle(fontSize: 13),
                      ),
                    ),
                  ),
                );
              },
            ),
        ],
      ),
    );
  }

  Widget _buildConnectRequestsScreen() {
    return Padding(
      padding: const EdgeInsets.all(20),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: const [
              Text('Connect Requests', style: TextStyle(fontSize: 22, fontWeight: FontWeight.bold, color: Color(0xFF0F172A))),
              SizedBox(height: 4),
              Text('Manage incoming and outgoing connection requests', style: TextStyle(color: Color(0xFF64748B), fontSize: 13)),
            ],
          ),
          const SizedBox(height: 20),
          Expanded(
            child: SingleChildScrollView(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text('Received Requests', style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold, color: Color(0xFF334155))),
                  const SizedBox(height: 12),
                  if (_pendingRequests.isEmpty)
                    Container(
                      width: double.infinity,
                      padding: const EdgeInsets.all(32),
                      decoration: BoxDecoration(
                        color: Colors.white,
                        borderRadius: BorderRadius.circular(16),
                        border: Border.all(color: const Color(0xFFE2E8F0)),
                      ),
                      child: Column(
                        mainAxisAlignment: MainAxisAlignment.center,
                        children: const [
                          Icon(Icons.inbox_outlined, size: 48, color: Color(0xFFCBD5E1)),
                          SizedBox(height: 12),
                          Text('No pending requests', style: TextStyle(fontWeight: FontWeight.bold, fontSize: 15, color: Color(0xFF475569))),
                          SizedBox(height: 4),
                          Text('When users send you connect requests, they will appear here.', style: TextStyle(color: Color(0xFF94A3B8), fontSize: 12), textAlign: TextAlign.center),
                        ],
                      ),
                    )
                  else
                    ListView.separated(
                      shrinkWrap: true,
                      physics: const NeverScrollableScrollPhysics(),
                      itemCount: _pendingRequests.length,
                      separatorBuilder: (_, __) => const SizedBox(height: 10),
                      itemBuilder: (context, index) {
                        final req = _pendingRequests[index];
                        final reqId = (req['id'] ?? req['requestId'])?.toString();
                        final senderHandle = (req['fromUserHandle'] ?? req['senderUserId'])?.toString() ?? 'Unknown';

                        return Card(
                          child: Padding(
                            padding: const EdgeInsets.all(14),
                            child: Row(
                              children: [
                                CircleAvatar(
                                  backgroundColor: const Color(0xFFFEF3C7),
                                  foregroundColor: const Color(0xFFD97706),
                                  child: Text(senderHandle.isNotEmpty ? senderHandle[0].toUpperCase() : 'R'),
                                ),
                                const SizedBox(width: 14),
                                Expanded(
                                  child: Column(
                                    crossAxisAlignment: CrossAxisAlignment.start,
                                    children: [
                                      Text('@$senderHandle', style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 15)),
                                      const SizedBox(height: 2),
                                      Text('Wants to connect with you', style: TextStyle(color: Colors.grey.shade600, fontSize: 12)),
                                    ],
                                  ),
                                ),
                                Row(
                                  mainAxisSize: MainAxisSize.min,
                                  children: [
                                    ElevatedButton(
                                      onPressed: reqId == null ? null : () => _acceptConnectRequest(reqId),
                                      style: ElevatedButton.styleFrom(
                                        backgroundColor: const Color(0xFF0D9488),
                                        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
                                      ),
                                      child: const Text('Accept'),
                                    ),
                                    const SizedBox(width: 8),
                                    OutlinedButton(
                                      onPressed: reqId == null ? null : () => _declineConnectRequest(reqId),
                                      style: OutlinedButton.styleFrom(
                                        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
                                        foregroundColor: const Color(0xFFE11D48),
                                        side: const BorderSide(color: Color(0xFFFECDD3)),
                                      ),
                                      child: const Text('Decline'),
                                    ),
                                  ],
                                ),
                              ],
                            ),
                          ),
                        );
                      },
                    ),
                  const SizedBox(height: 28),
                  const Text('Sent Requests', style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold, color: Color(0xFF334155))),
                  const SizedBox(height: 12),
                  if (_sentRequests.isEmpty)
                    Container(
                      width: double.infinity,
                      padding: const EdgeInsets.all(32),
                      decoration: BoxDecoration(
                        color: Colors.white,
                        borderRadius: BorderRadius.circular(16),
                        border: Border.all(color: const Color(0xFFE2E8F0)),
                      ),
                      child: Column(
                        mainAxisAlignment: MainAxisAlignment.center,
                        children: const [
                          Icon(Icons.outbox_outlined, size: 48, color: Color(0xFFCBD5E1)),
                          SizedBox(height: 12),
                          Text('No sent requests', style: TextStyle(fontWeight: FontWeight.bold, fontSize: 15, color: Color(0xFF475569))),
                          SizedBox(height: 4),
                          Text('Connect requests you send to other users will be tracked here.', style: TextStyle(color: Color(0xFF94A3B8), fontSize: 12), textAlign: TextAlign.center),
                        ],
                      ),
                    )
                  else
                    ListView.separated(
                      shrinkWrap: true,
                      physics: const NeverScrollableScrollPhysics(),
                      itemCount: _sentRequests.length,
                      separatorBuilder: (_, __) => const SizedBox(height: 10),
                      itemBuilder: (context, index) {
                        final req = _sentRequests[index];
                        final targetHandle = req['targetHandle'] ?? 'User';

                        return Card(
                          child: Padding(
                            padding: const EdgeInsets.all(14),
                            child: Row(
                              children: [
                                CircleAvatar(
                                  backgroundColor: const Color(0xFFEFF6FF),
                                  foregroundColor: const Color(0xFF2563EB),
                                  child: Text(targetHandle.isNotEmpty ? targetHandle[0].toUpperCase() : 'S'),
                                ),
                                const SizedBox(width: 14),
                                Expanded(
                                  child: Column(
                                    crossAxisAlignment: CrossAxisAlignment.start,
                                    children: [
                                      Text('@$targetHandle', style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 15)),
                                      const SizedBox(height: 2),
                                      const Text('Request sent', style: TextStyle(color: Color(0xFF64748B), fontSize: 12)),
                                    ],
                                  ),
                                ),
                                Container(
                                  padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
                                  decoration: BoxDecoration(
                                    color: const Color(0xFFF1F5F9),
                                    borderRadius: BorderRadius.circular(8),
                                  ),
                                  child: const Text('Pending', style: TextStyle(fontSize: 12, color: Color(0xFF64748B), fontWeight: FontWeight.w600)),
                                ),
                              ],
                            ),
                          ),
                        );
                      },
                    ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildCallingScreen() {
    return SingleChildScrollView(
      padding: const EdgeInsets.all(20),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: const [
                  Text('Voice Calling', style: TextStyle(fontSize: 22, fontWeight: FontWeight.bold, color: Color(0xFF0F172A))),
                  SizedBox(height: 4),
                  Text('Quick call dialing for your connected contacts', style: TextStyle(color: Color(0xFF64748B), fontSize: 13)),
                ],
              ),
            ],
          ),
          const SizedBox(height: 20),
          const Text('Quick Call Dialing', style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold, color: Color(0xFF334155))),
          const SizedBox(height: 12),
          if (_connections.isEmpty)
            Container(
              padding: const EdgeInsets.all(24),
              decoration: BoxDecoration(
                color: Colors.white,
                borderRadius: BorderRadius.circular(16),
                border: Border.all(color: const Color(0xFFE2E8F0)),
              ),
              alignment: Alignment.center,
              child: const Text('No contacts available to call. Connect with users first.', style: TextStyle(color: Color(0xFF64748B))),
            )
          else
            ListView.separated(
              shrinkWrap: true,
              physics: const NeverScrollableScrollPhysics(),
              itemCount: _connections.length,
              separatorBuilder: (_, __) => const SizedBox(height: 10),
              itemBuilder: (context, index) {
                final conn = _connections[index];
                final targetGuidId = conn['contactId'] ?? conn['connectedUserId'];
                final handle = conn['contactUserId'] ?? conn['userId'] ?? 'User';

                return Card(
                  child: ListTile(
                    leading: CircleAvatar(
                      backgroundColor: const Color(0xFFCCFBF1),
                      foregroundColor: const Color(0xFF0D9488),
                      child: Text(handle.isNotEmpty ? handle[0].toUpperCase() : 'U'),
                    ),
                    title: Text('@$handle', style: const TextStyle(fontWeight: FontWeight.bold)),
                    subtitle: const Text('Voice Call via SignalR', style: TextStyle(fontSize: 12, color: Color(0xFF64748B))),
                    trailing: ElevatedButton.icon(
                      onPressed: () => _initiateCall(targetGuidId, handle),
                      icon: const Icon(Icons.phone, size: 18),
                      label: const Text('Initiate Call'),
                    ),
                  ),
                );
              },
            ),
        ],
      ),
    );
  }

  Widget _buildCallTopBanner() {
    return Container(
      color: _isActiveCall
          ? const Color(0xFF059669)
          : _isIncomingCall
              ? const Color(0xFFD97706)
              : const Color(0xFF2563EB),
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Row(
            children: [
              Icon(
                _isActiveCall
                    ? Icons.phone_in_talk
                    : _isIncomingCall
                        ? Icons.ring_volume
                        : Icons.phone_callback,
                color: Colors.white,
              ),
              const SizedBox(width: 12),
              Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    _callStatusText,
                    style: const TextStyle(fontWeight: FontWeight.bold, color: Colors.white, fontSize: 14),
                  ),
                  if (_isActiveCall)
                    Text('Duration: ${_callTimerSeconds}s', style: const TextStyle(color: Colors.white70, fontSize: 12)),
                  if (_isIncomingCall || _isRinging)
                    Text('Timeout: ${_ringTimerSeconds}s', style: const TextStyle(color: Colors.white70, fontSize: 12)),
                ],
              ),
            ],
          ),
          Row(
            children: [
              if (_isIncomingCall) ...[
                ElevatedButton(
                  onPressed: () => _respondToCall(true),
                  style: ElevatedButton.styleFrom(backgroundColor: Colors.white, foregroundColor: const Color(0xFF059669)),
                  child: const Text('Accept'),
                ),
                const SizedBox(width: 8),
                ElevatedButton(
                  onPressed: () => _respondToCall(false),
                  style: ElevatedButton.styleFrom(backgroundColor: const Color(0xFFDC2626), foregroundColor: Colors.white),
                  child: const Text('Decline'),
                ),
              ],
              if (_isActiveCall || _isRinging)
                ElevatedButton.icon(
                  onPressed: _endCall,
                  icon: const Icon(Icons.call_end, size: 18),
                  label: const Text('End Call'),
                  style: ElevatedButton.styleFrom(backgroundColor: const Color(0xFFDC2626), foregroundColor: Colors.white),
                ),
            ],
          ),
        ],
      ),
    );
  }

  Widget _buildFullCallOverlay() {
    return Container(
      color: const Color(0xF50F172A),
      padding: const EdgeInsets.all(32),
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Container(
            width: 120,
            height: 120,
            decoration: BoxDecoration(
              shape: BoxShape.circle,
              color: const Color(0x330D9488),
              border: Border.all(color: const Color(0xFF0D9488), width: 3),
            ),
            child: Center(
              child: Text(
                (_callerOrCalleeName != null && _callerOrCalleeName!.isNotEmpty)
                    ? _callerOrCalleeName![0].toUpperCase()
                    : 'C',
                style: const TextStyle(fontSize: 48, fontWeight: FontWeight.bold, color: Colors.white),
              ),
            ),
          ),
          const SizedBox(height: 24),
          Text(
            '@${_callerOrCalleeName ?? "Unknown"}',
            style: const TextStyle(fontSize: 28, fontWeight: FontWeight.bold, color: Colors.white),
          ),
          const SizedBox(height: 8),
          Text(
            _isActiveCall ? 'Call Active' : 'Incoming Voice Call...',
            style: TextStyle(fontSize: 16, color: Colors.teal.shade200),
          ),
          const SizedBox(height: 12),
          if (_isActiveCall)
            Text(
              '${(_callTimerSeconds ~/ 60).toString().padLeft(2, '0')}:${(_callTimerSeconds % 60).toString().padLeft(2, '0')}',
              style: const TextStyle(fontSize: 36, fontFamily: 'monospace', color: Colors.white),
            ),
          if (_isIncomingCall)
            Text(
              'Ringing... (${_ringTimerSeconds}s remaining)',
              style: const TextStyle(fontSize: 14, color: Colors.amberAccent),
            ),
          const SizedBox(height: 40),
          Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              Tooltip(
                message: 'Audio Mute (Inert - Sprint 7.6)',
                child: Opacity(
                  opacity: 0.4,
                  child: Container(
                    padding: const EdgeInsets.all(16),
                    decoration: const BoxDecoration(color: Color(0xFF334155), shape: BoxShape.circle),
                    child: const Icon(Icons.mic_off, color: Colors.white, size: 28),
                  ),
                ),
              ),
              const SizedBox(width: 24),
              Tooltip(
                message: 'Speakerphone (Inert - Sprint 7.6)',
                child: Opacity(
                  opacity: 0.4,
                  child: Container(
                    padding: const EdgeInsets.all(16),
                    decoration: const BoxDecoration(color: Color(0xFF334155), shape: BoxShape.circle),
                    child: const Icon(Icons.volume_up, color: Colors.white, size: 28),
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: 12),
          const Text(
            'Audio controls inert (Sprint 7.6 audio integration)',
            style: TextStyle(color: Color(0xFF64748B), fontSize: 11, fontStyle: FontStyle.italic),
          ),
          const SizedBox(height: 48),
          if (_isIncomingCall)
            Row(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                FloatingActionButton.large(
                  onPressed: () => _respondToCall(true),
                  backgroundColor: const Color(0xFF10B981),
                  child: const Icon(Icons.call, size: 36, color: Colors.white),
                ),
                const SizedBox(width: 48),
                FloatingActionButton.large(
                  onPressed: () => _respondToCall(false),
                  backgroundColor: const Color(0xFFE11D48),
                  child: const Icon(Icons.call_end, size: 36, color: Colors.white),
                ),
              ],
            )
          else
            FloatingActionButton.large(
              onPressed: _endCall,
              backgroundColor: const Color(0xFFE11D48),
              child: const Icon(Icons.call_end, size: 36, color: Colors.white),
            ),
        ],
      ),
    );
  }

  Widget _buildCallHistoryScreen() {
    return Padding(
      padding: const EdgeInsets.all(20),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: const [
              Text('Call History', style: TextStyle(fontSize: 22, fontWeight: FontWeight.bold, color: Color(0xFF0F172A))),
              SizedBox(height: 4),
              Text('Recent voice call logs and status', style: TextStyle(color: Color(0xFF64748B), fontSize: 13)),
            ],
          ),
          const SizedBox(height: 20),
          Expanded(
            child: _callHistory.isEmpty
                ? Container(
                    width: double.infinity,
                    padding: const EdgeInsets.all(40),
                    decoration: BoxDecoration(
                      color: Colors.white,
                      borderRadius: BorderRadius.circular(16),
                      border: Border.all(color: const Color(0xFFE2E8F0)),
                    ),
                    child: Column(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: const [
                        Icon(Icons.history_toggle_off, size: 56, color: Color(0xFFCBD5E1)),
                        SizedBox(height: 16),
                        Text('No call history records', style: TextStyle(fontWeight: FontWeight.bold, fontSize: 16, color: Color(0xFF475569))),
                        SizedBox(height: 6),
                        Text('Completed, rejected, or missed calls will be listed here.', style: TextStyle(color: Color(0xFF94A3B8), fontSize: 13), textAlign: TextAlign.center),
                      ],
                    ),
                  )
                : ListView.separated(
                    itemCount: _callHistory.length,
                    separatorBuilder: (_, __) => const SizedBox(height: 10),
                    itemBuilder: (context, index) {
                      final item = _callHistory[index];
                      final isOutgoing = item['isOutgoing'] ?? false;
                      final callerUserId = item['callerUserId'] ?? 'Unknown';
                      final calleeUserId = item['calleeUserId'] ?? 'Unknown';
                      final status = item['status'] ?? 'Unknown';
                      final reason = item['missedReason'];
                      final duration = item['durationSeconds'] ?? 0;
                      final startedAt = item['startedAt'] ?? '';

                      final otherPerson = isOutgoing ? calleeUserId : callerUserId;
                      final bool isAccepted = status == 'Accepted';

                      return Card(
                        child: ListTile(
                          leading: CircleAvatar(
                            backgroundColor: isAccepted
                                ? (isOutgoing ? const Color(0xFFECFDF5) : const Color(0xFFEFF6FF))
                                : const Color(0xFFFFF1F2),
                            foregroundColor: isAccepted
                                ? (isOutgoing ? const Color(0xFF059669) : const Color(0xFF2563EB))
                                : const Color(0xFFE11D48),
                            child: Icon(
                              isAccepted
                                  ? (isOutgoing ? Icons.call_made : Icons.call_received)
                                  : Icons.call_missed,
                              size: 20,
                            ),
                          ),
                          title: Text('@$otherPerson', style: const TextStyle(fontWeight: FontWeight.bold)),
                          subtitle: Text(
                            '${isOutgoing ? "Outgoing" : "Incoming"} • Status: $status ${reason != null ? "($reason)" : ""}\nTime: $startedAt',
                            style: const TextStyle(fontSize: 12, color: Color(0xFF64748B)),
                          ),
                          trailing: Text(
                            '${duration}s',
                            style: const TextStyle(fontFamily: 'monospace', color: Color(0xFF475569)),
                          ),
                        ),
                      );
                    },
                  ),
          ),
        ],
      ),
    );
  }

  Widget _buildAccountSettingsScreen() {
    final session = currentSession;

    return SingleChildScrollView(
      padding: const EdgeInsets.all(20),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text('Settings', style: TextStyle(fontSize: 22, fontWeight: FontWeight.bold, color: Color(0xFF0F172A))),
          const SizedBox(height: 4),
          const Text('Manage privacy, safety, and administrative account options', style: TextStyle(color: Color(0xFF64748B), fontSize: 13)),
          const SizedBox(height: 24),

          // Privacy & Safety Section
          const Text('Privacy & Safety', style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold, color: Color(0xFF334155))),
          const SizedBox(height: 12),
          Card(
            child: Padding(
              padding: const EdgeInsets.all(20),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text('Blocked Users', style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold, color: Color(0xFF0F172A))),
                  const SizedBox(height: 10),
                  if (_blockedUsers.isEmpty)
                    Container(
                      width: double.infinity,
                      padding: const EdgeInsets.symmetric(vertical: 24, horizontal: 16),
                      decoration: BoxDecoration(
                        color: const Color(0xFFF8FAFC),
                        borderRadius: BorderRadius.circular(12),
                        border: Border.all(color: const Color(0xFFE2E8F0)),
                      ),
                      child: Column(
                        children: const [
                          Icon(Icons.shield_outlined, size: 36, color: Color(0xFFCBD5E1)),
                          SizedBox(height: 8),
                          Text(
                            "You haven't blocked anyone.",
                            style: TextStyle(color: Color(0xFF64748B), fontSize: 14, fontWeight: FontWeight.w500),
                          ),
                        ],
                      ),
                    )
                  else
                    ListView.separated(
                      shrinkWrap: true,
                      physics: const NeverScrollableScrollPhysics(),
                      itemCount: _blockedUsers.length,
                      separatorBuilder: (_, __) => const SizedBox(height: 8),
                      itemBuilder: (context, index) {
                        final b = _blockedUsers[index];
                        final id = b['userId'] ?? b['blockedUserId'];
                        final handle = b['handle'] ?? b['userId'] ?? 'Blocked User';

                        return Card(
                          child: ListTile(
                            leading: CircleAvatar(
                              backgroundColor: const Color(0xFFFFF1F2),
                              foregroundColor: const Color(0xFFE11D48),
                              child: Text(handle.isNotEmpty ? handle[0].toUpperCase() : 'B'),
                            ),
                            title: Text('@$handle', style: const TextStyle(fontWeight: FontWeight.bold)),
                            trailing: OutlinedButton(
                              onPressed: () => _unblockUser(id),
                              child: const Text('Unblock'),
                            ),
                          ),
                        );
                      },
                    ),
                  const SizedBox(height: 20),
                  const Divider(),
                  const SizedBox(height: 16),
                  const Text('Safety Reporting', style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold, color: Color(0xFF0F172A))),
                  const SizedBox(height: 6),
                  const Text('Report inappropriate behavior or harassment directly to administrators.', style: TextStyle(color: Color(0xFF64748B), fontSize: 13)),
                  const SizedBox(height: 14),
                  ElevatedButton.icon(
                    onPressed: _showReportUserDialog,
                    icon: const Icon(Icons.flag_outlined, size: 18),
                    label: const Text('Report a User'),
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 28),

          // Account Section
          const Text('Account', style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold, color: Color(0xFF334155))),
          const SizedBox(height: 12),
          Card(
            child: Padding(
              padding: const EdgeInsets.all(20),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text('Account Summary', style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold, color: Color(0xFF0F172A))),
                  const SizedBox(height: 12),
                  Row(
                    children: [
                      const Text('User Handle: ', style: TextStyle(fontWeight: FontWeight.bold, color: Color(0xFF475569))),
                      Text('@${session?.handle ?? "Unknown"}', style: const TextStyle(color: Color(0xFF0F172A))),
                    ],
                  ),
                  const SizedBox(height: 6),
                  Row(
                    children: [
                      const Text('Email Address: ', style: TextStyle(fontWeight: FontWeight.bold, color: Color(0xFF475569))),
                      Text(session?.email ?? 'No email address', style: const TextStyle(color: Color(0xFF0F172A))),
                    ],
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 20),

          // Danger Zone Block
          Card(
            color: const Color(0xFFFFF1F2),
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(16),
              side: const BorderSide(color: Color(0xFFFECDD3), width: 1.5),
            ),
            child: Padding(
              padding: const EdgeInsets.all(20),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text('Danger Zone', style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold, color: Color(0xFFBE123C))),
                  const SizedBox(height: 6),
                  const Text(
                    'Soft-deleting your account flags it for deletion with a 60-day recovery window. Logging back in during this window automatically reactivates your account.',
                    style: TextStyle(color: Color(0xFF9F1239), fontSize: 13),
                  ),
                  const SizedBox(height: 16),
                  ElevatedButton.icon(
                    onPressed: _showDeleteConfirmationDialog,
                    icon: const Icon(Icons.delete_forever, size: 18),
                    label: const Text('Delete Account'),
                    style: ElevatedButton.styleFrom(backgroundColor: const Color(0xFFE11D48)),
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  void _showDeleteConfirmationDialog() {
    showDialog(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Delete Account?'),
        content: const Text(
          'Are you sure you want to delete your account?\n\nYour account will be flagged as deleted with a 60-day recovery window. Logging in within 60 days will reactivate it.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(ctx).pop(),
            child: const Text('Cancel'),
          ),
          ElevatedButton(
            onPressed: () {
              Navigator.of(ctx).pop();
              _softDeleteAccount();
            },
            style: ElevatedButton.styleFrom(backgroundColor: const Color(0xFFE11D48)),
            child: const Text('Confirm Delete'),
          ),
        ],
      ),
    );
  }

  Widget _buildDevConsoleSheet() {
    final session = currentSession;

    return Container(
      color: const Color(0xEB000000),
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Row(
                children: const [
                  Icon(Icons.terminal, color: Color(0xFF4ADE80), size: 20),
                  SizedBox(width: 8),
                  Text(
                    'Developer Tools & Test Harness',
                    style: TextStyle(color: Color(0xFF4ADE80), fontWeight: FontWeight.bold, fontSize: 14),
                  ),
                ],
              ),
              IconButton(
                icon: const Icon(Icons.close, color: Colors.white70),
                onPressed: () => setState(() => _showDevConsole = false),
              ),
            ],
          ),
          const SizedBox(height: 8),
          Container(
            padding: const EdgeInsets.all(10),
            decoration: BoxDecoration(
              color: const Color(0xFF1E293B),
              borderRadius: BorderRadius.circular(10),
              border: Border.all(color: const Color(0xFF334155)),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Row(
                      children: [
                        Icon(
                          _isHubConnected ? Icons.check_circle_rounded : Icons.cancel_rounded,
                          color: _isHubConnected ? const Color(0xFF10B981) : const Color(0xFFE11D48),
                          size: 18,
                        ),
                        const SizedBox(width: 8),
                        Text(
                          _isHubConnected ? 'SignalR Active' : 'SignalR Disconnected',
                          style: TextStyle(color: _isHubConnected ? const Color(0xFF4ADE80) : const Color(0xFFF87171), fontSize: 13, fontWeight: FontWeight.bold),
                        ),
                      ],
                    ),
                    OutlinedButton.icon(
                      onPressed: _connectSignalR,
                      icon: const Icon(Icons.sync, size: 14, color: Colors.white70),
                      label: const Text('Reconnect SignalR', style: TextStyle(color: Colors.white70, fontSize: 12)),
                      style: OutlinedButton.styleFrom(
                        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
                        side: const BorderSide(color: Color(0xFF475569)),
                      ),
                    ),
                  ],
                ),
                if (session != null) ...[
                  const SizedBox(height: 6),
                  Text(
                    'Dev Debug GUID: ${session.id}',
                    style: const TextStyle(fontFamily: 'monospace', fontSize: 11, color: Color(0xFF94A3B8)),
                  ),
                ],
              ],
            ),
          ),
          const SizedBox(height: 8),
          Container(
            padding: const EdgeInsets.all(10),
            decoration: BoxDecoration(
              color: const Color(0xFF1E293B),
              borderRadius: BorderRadius.circular(10),
              border: Border.all(color: const Color(0xFF334155)),
            ),
            child: Row(
              children: [
                const Icon(Icons.info_outline, color: Color(0xFF38BDF8), size: 18),
                const SizedBox(width: 8),
                const Expanded(
                  child: Text(
                    'Dual User Slot Switcher (Dev Harness Convenience)',
                    style: TextStyle(color: Color(0xFF94A3B8), fontSize: 12),
                  ),
                ),
                ChoiceChip(
                  label: Text('User 1 (${_user1Session?.handle ?? "Empty"})'),
                  selected: _activeSessionIndex == 1,
                  onSelected: (_) => _switchSession(1),
                ),
                const SizedBox(width: 8),
                ChoiceChip(
                  label: Text('User 2 (${_user2Session?.handle ?? "Empty"})'),
                  selected: _activeSessionIndex == 2,
                  onSelected: (_) => _switchSession(2),
                ),
              ],
            ),
          ),
          const SizedBox(height: 12),
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              const Text('Console Audit Trail:', style: TextStyle(color: Colors.white70, fontSize: 12)),
              TextButton(
                onPressed: () => setState(() => _consoleLogs.clear()),
                child: const Text('Clear Logs', style: TextStyle(color: Colors.white54, fontSize: 11)),
              ),
            ],
          ),
          Expanded(
            child: Container(
              padding: const EdgeInsets.all(10),
              decoration: BoxDecoration(
                color: const Color(0xFF020617),
                borderRadius: BorderRadius.circular(8),
                border: Border.all(color: const Color(0xFF1E293B)),
              ),
              child: ListView.builder(
                itemCount: _consoleLogs.length,
                itemBuilder: (context, index) {
                  return SelectableText(
                    _consoleLogs[index],
                    style: const TextStyle(fontFamily: 'monospace', fontSize: 11, color: Color(0xFF4ADE80)),
                  );
                },
              ),
            ),
          ),
        ],
      ),
    );
  }
}
