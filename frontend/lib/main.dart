import 'dart:async';
import 'dart:convert';
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
      title: 'Connect E2E Test Client',
      debugShowCheckedModeBanner: false,
      theme: ThemeData(
        brightness: Brightness.dark,
        scaffoldBackgroundColor: const Color(0xFF0F172A),
        colorScheme: const ColorScheme.dark(
          primary: Color(0xFF6366F1),
          secondary: Color(0xFF06B6D4),
          surface: Color(0xFF1E293B),
          error: Color(0xFFF43F5E),
        ),
        useMaterial3: true,
      ),
      home: const MainTestDashboard(),
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

class MainTestDashboard extends StatefulWidget {
  const MainTestDashboard({super.key});

  @override
  State<MainTestDashboard> createState() => _MainTestDashboardState();
}

class _MainTestDashboardState extends State<MainTestDashboard> with SingleTickerProviderStateMixin {
  final String _baseUrl = 'http://localhost:5200';

  UserSession? _user1Session;
  UserSession? _user2Session;
  int _activeSessionIndex = 1;

  UserSession? get currentSession => _activeSessionIndex == 1 ? _user1Session : _user2Session;

  late TabController _tabController;

  HubConnection? _hubConnection;
  bool _isHubConnected = false;

  TextEditingController _searchQueryController = TextEditingController();
  List<dynamic> _searchResults = [];
  bool _isSearching = false;

  List<dynamic> _pendingRequests = [];
  List<dynamic> _connections = [];
  List<dynamic> _blockedUsers = [];
  List<dynamic> _callHistory = [];

  TextEditingController _handleCheckController = TextEditingController();
  String? _handleCheckResult;

  TextEditingController _regEmailController = TextEditingController(text: 'user1@connect.com');
  TextEditingController _regPasswordController = TextEditingController(text: 'Password123!');
  TextEditingController _regHandleController = TextEditingController(text: 'user_one');
  TextEditingController _regPhoneController = TextEditingController(text: '1112223333');

  TextEditingController _loginEmailController = TextEditingController(text: 'user1@connect.com');
  TextEditingController _loginPasswordController = TextEditingController(text: 'Password123!');

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

  TextEditingController _reportReasonController = TextEditingController();
  TextEditingController _reportNoteController = TextEditingController();

  List<String> _consoleLogs = [];

  void _log(String message) {
    final timestamp = DateTime.now().toIso8601String().substring(11, 19);
    setState(() {
      _consoleLogs.insert(0, '[$timestamp] $message');
    });
  }

  @override
  void initState() {
    super.initState();
    _tabController = TabController(length: 6, vsync: this);
  }

  @override
  void dispose() {
    _tabController.dispose();
    _hubConnection?.stop();
    _callTimer?.cancel();
    _ringTimer?.cancel();
    _searchQueryController.dispose();
    _handleCheckController.dispose();
    _regEmailController.dispose();
    _regPasswordController.dispose();
    _regHandleController.dispose();
    _regPhoneController.dispose();
    _loginEmailController.dispose();
    _loginPasswordController.dispose();
    _reportReasonController.dispose();
    _reportNoteController.dispose();
    super.dispose();
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
    final value = _handleCheckController.text.trim();
    if (value.isEmpty) return;

    try {
      final res = await http.get(Uri.parse('$_baseUrl/api/v1/users/check-userid?value=$value'));
      final data = jsonDecode(res.body);
      setState(() {
        _handleCheckResult = res.statusCode == 200
            ? 'Available: ${data['isAvailable']} (${data['message'] ?? ''})'
            : 'Error: ${res.statusCode}';
      });
      _log('Check Handle ($value): ${res.body}');
    } catch (e) {
      _log('Check Handle Exception: $e');
    }
  }

  Future<void> _registerUser(int sessionSlot) async {
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
        setState(() {
          if (sessionSlot == 1) {
            _user1Session = session;
          } else {
            _user2Session = session;
          }
          _activeSessionIndex = sessionSlot;
        });
        await _connectSignalR();
        _refreshActiveTabData();
      }
    } catch (e) {
      _log('Register Exception: $e');
    }
  }

  Future<void> _loginUser(int sessionSlot) async {
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
        setState(() {
          if (sessionSlot == 1) {
            _user1Session = session;
          } else {
            _user2Session = session;
          }
          _activeSessionIndex = sessionSlot;
        });
        await _connectSignalR();
        _refreshActiveTabData();
      }
    } catch (e) {
      _log('Login Exception: $e');
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

  Future<void> _sendConnectRequest(String targetGuidId) async {
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
      _fetchPendingRequests();
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
    if (session == null) return;

    try {
      final res = await http.post(
        Uri.parse('$_baseUrl/api/v1/connect-requests/$requestId/accept'),
        headers: {'Authorization': 'Bearer ${session.token}'},
      );

      _log('Accept Request $requestId -> Status ${res.statusCode}: ${res.body}');
      _fetchPendingRequests();
      _fetchConnections();
    } catch (e) {
      _log('Accept Request Exception: $e');
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

      if (res.statusCode == 200) {
        setState(() {
          _connections = jsonDecode(res.body);
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
      _fetchBlockedUsers();
      _fetchConnections();
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
      _fetchBlockedUsers();
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
      _reportReasonController.clear();
      _reportNoteController.clear();
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
      if (res.statusCode == 204 || res.statusCode == 200) {
        _log('Account soft-deleted. Log in again within 60 days to test silent reactivation.');
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
    await _connectSignalR();
    _refreshActiveTabData();
  }

  @override
  Widget build(BuildContext context) {
    final session = currentSession;

    return Scaffold(
      appBar: AppBar(
        title: Text('Connect E2E Functional Tester (${session != null ? session.handle : "No Session"})'),
        bottom: TabBar(
          controller: _tabController,
          isScrollable: true,
          tabs: const [
            Tab(text: '1. Auth & Users'),
            Tab(text: '2. Directory & Search'),
            Tab(text: '3. Connect Requests'),
            Tab(text: '4. Calling & SignalR'),
            Tab(text: '5. Call History'),
            Tab(text: '6. Trust, Safety & Account'),
          ],
        ),
        actions: [
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 8.0),
            child: Row(
              children: [
                ChoiceChip(
                  label: Text('User 1 (${_user1Session != null ? _user1Session!.handle : "Empty"})'),
                  selected: _activeSessionIndex == 1,
                  onSelected: (val) => _switchSession(1),
                ),
                const SizedBox(width: 8),
                ChoiceChip(
                  label: Text('User 2 (${_user2Session != null ? _user2Session!.handle : "Empty"})'),
                  selected: _activeSessionIndex == 2,
                  onSelected: (val) => _switchSession(2),
                ),
              ],
            ),
          )
        ],
      ),
      body: Column(
        children: [
          if (_isIncomingCall || _isRinging || _isActiveCall || _activeCallId != null)
            _buildCallOverlayBar(),

          Expanded(
            child: TabBarView(
              controller: _tabController,
              children: [
                _buildAuthTab(),
                _buildDirectoryTab(),
                _buildConnectRequestsTab(),
                _buildCallingTab(),
                _buildCallHistoryTab(),
                _buildTrustAndSafetyTab(),
              ],
            ),
          ),

          _buildConsoleFooter(),
        ],
      ),
    );
  }

  Widget _buildCallOverlayBar() {
    return Container(
      color: _isActiveCall
          ? Colors.green.shade900
          : _isIncomingCall
              ? Colors.amber.shade900
              : Colors.indigo.shade900,
      padding: const EdgeInsets.all(12),
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
                    style: const TextStyle(fontWeight: FontWeight.bold, color: Colors.white),
                  ),
                  if (_isActiveCall)
                    Text('Duration: ${_callTimerSeconds}s', style: const TextStyle(color: Colors.white70)),
                  if (_isIncomingCall || _isRinging)
                    Text('Ring Timeout: ${_ringTimerSeconds}s', style: const TextStyle(color: Colors.white70)),
                ],
              ),
            ],
          ),
          Row(
            children: [
              if (_isIncomingCall) ...[
                ElevatedButton.icon(
                  onPressed: () => _respondToCall(true),
                  icon: const Icon(Icons.call),
                  label: const Text('Accept'),
                  style: ElevatedButton.styleFrom(backgroundColor: Colors.green),
                ),
                const SizedBox(width: 8),
                ElevatedButton.icon(
                  onPressed: () => _respondToCall(false),
                  icon: const Icon(Icons.call_end),
                  label: const Text('Decline'),
                  style: ElevatedButton.styleFrom(backgroundColor: Colors.red),
                ),
              ],
              if (_isActiveCall || _isRinging)
                ElevatedButton.icon(
                  onPressed: _endCall,
                  icon: const Icon(Icons.call_end),
                  label: const Text('End Call'),
                  style: ElevatedButton.styleFrom(backgroundColor: Colors.red),
                ),
            ],
          )
        ],
      ),
    );
  }

  Widget _buildAuthTab() {
    return SingleChildScrollView(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text('Step 1: User Handle Availability Check', style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold)),
          const SizedBox(height: 8),
          Row(
            children: [
              Expanded(
                child: TextField(
                  controller: _handleCheckController,
                  decoration: const InputDecoration(labelText: 'User ID Handle (e.g. user_one)', border: OutlineInputBorder()),
                ),
              ),
              const SizedBox(width: 12),
              ElevatedButton(
                onPressed: _checkHandleAvailability,
                child: const Text('Check Availability'),
              ),
            ],
          ),
          if (_handleCheckResult != null)
            Padding(
              padding: const EdgeInsets.all(8.0),
              child: Text(_handleCheckResult!, style: const TextStyle(color: Colors.cyan)),
            ),
          const Divider(height: 32),

          const Text('Register User into Selected Slot', style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold)),
          const SizedBox(height: 8),
          TextField(controller: _regHandleController, decoration: const InputDecoration(labelText: 'User ID (@handle)', border: OutlineInputBorder())),
          const SizedBox(height: 8),
          TextField(controller: _regEmailController, decoration: const InputDecoration(labelText: 'Email', border: OutlineInputBorder())),
          const SizedBox(height: 8),
          TextField(controller: _regPasswordController, decoration: const InputDecoration(labelText: 'Password', border: OutlineInputBorder())),
          const SizedBox(height: 8),
          TextField(controller: _regPhoneController, decoration: const InputDecoration(labelText: 'Phone Number', border: OutlineInputBorder())),
          const SizedBox(height: 12),
          Row(
            children: [
              ElevatedButton(
                onPressed: () => _registerUser(1),
                child: const Text('Register as User 1'),
              ),
              const SizedBox(width: 12),
              ElevatedButton(
                onPressed: () => _registerUser(2),
                child: const Text('Register as User 2'),
              ),
            ],
          ),
          const Divider(height: 32),

          const Text('Login Existing User into Selected Slot', style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold)),
          const SizedBox(height: 8),
          TextField(controller: _loginEmailController, decoration: const InputDecoration(labelText: 'Email or User ID', border: OutlineInputBorder())),
          const SizedBox(height: 8),
          TextField(controller: _loginPasswordController, decoration: const InputDecoration(labelText: 'Password', border: OutlineInputBorder())),
          const SizedBox(height: 12),
          Row(
            children: [
              ElevatedButton(
                onPressed: () => _loginUser(1),
                child: const Text('Login into User 1 Slot'),
              ),
              const SizedBox(width: 12),
              ElevatedButton(
                onPressed: () => _loginUser(2),
                child: const Text('Login into User 2 Slot'),
              ),
            ],
          ),
        ],
      ),
    );
  }

  Widget _buildDirectoryTab() {
    return Padding(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text('Step 2: Search Users by User ID or Phone Number', style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold)),
          const SizedBox(height: 8),
          Row(
            children: [
              Expanded(
                child: TextField(
                  controller: _searchQueryController,
                  decoration: const InputDecoration(labelText: 'Query (User ID handle or phone number)', border: OutlineInputBorder()),
                ),
              ),
              const SizedBox(width: 12),
              ElevatedButton(
                onPressed: _searchUsers,
                child: const Text('Search'),
              ),
            ],
          ),
          const SizedBox(height: 16),
          _isSearching
              ? const CircularProgressIndicator()
              : Expanded(
                  child: ListView.builder(
                    itemCount: _searchResults.length,
                    itemBuilder: (context, index) {
                      final item = _searchResults[index];
                      final guidId = item['id'];
                      final handle = item['userId'];
                      final phone = item['phoneNumber'];

                      return Card(
                        child: ListTile(
                          title: Text(handle ?? 'User'),
                          subtitle: Text('Phone: $phone | Guid ID: $guidId'),
                          trailing: ElevatedButton(
                            onPressed: () => _sendConnectRequest(guidId),
                            child: const Text('Send Connect Request'),
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

  Widget _buildConnectRequestsTab() {
    return Padding(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              const Text('Step 3: Pending Connect Requests & Active Connections', style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold)),
              IconButton(icon: const Icon(Icons.refresh), onPressed: _refreshActiveTabData),
            ],
          ),
          const SizedBox(height: 8),
          const Text('Pending Connect Requests Received:', style: TextStyle(fontWeight: FontWeight.bold, color: Colors.amber)),
          SizedBox(
            height: 180,
            child: _pendingRequests.isEmpty
                ? const Center(child: Text('No pending connect requests.'))
                : ListView.builder(
                    itemCount: _pendingRequests.length,
                    itemBuilder: (context, index) {
                      final req = _pendingRequests[index];
                      final reqId = req['requestId'];
                      final senderHandle = req['senderUserId'];

                      return Card(
                        child: ListTile(
                          title: Text('From: $senderHandle'),
                          subtitle: Text('Request ID: $reqId'),
                          trailing: Row(
                            mainAxisSize: MainAxisSize.min,
                            children: [
                              ElevatedButton(
                                onPressed: () => _acceptConnectRequest(reqId),
                                style: ElevatedButton.styleFrom(backgroundColor: Colors.green),
                                child: const Text('Accept'),
                              ),
                            ],
                          ),
                        ),
                      );
                    },
                  ),
          ),
          const Divider(height: 24),
          const Text('My Connected Contacts:', style: TextStyle(fontWeight: FontWeight.bold, color: Colors.lightBlue)),
          Expanded(
            child: _connections.isEmpty
                ? const Center(child: Text('No connections yet. Send & accept requests first.'))
                : ListView.builder(
                    itemCount: _connections.length,
                    itemBuilder: (context, index) {
                      final conn = _connections[index];
                      final targetGuidId = conn['connectedUserId'];
                      final handle = conn['userId'];
                      final presence = conn['presenceStatus'];

                      return Card(
                        child: ListTile(
                          title: Text(handle ?? 'Connected Contact'),
                          subtitle: Text('Status: $presence | Guid ID: $targetGuidId'),
                          trailing: Row(
                            mainAxisSize: MainAxisSize.min,
                            children: [
                              ElevatedButton.icon(
                                onPressed: () => _initiateCall(targetGuidId, handle),
                                icon: const Icon(Icons.call),
                                label: const Text('Voice Call'),
                              ),
                              const SizedBox(width: 8),
                              IconButton(
                                icon: const Icon(Icons.block, color: Colors.red),
                                tooltip: 'Block User',
                                onPressed: () => _blockUser(targetGuidId),
                              ),
                            ],
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

  Widget _buildCallingTab() {
    return SingleChildScrollView(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text('Step 4 & 6: Voice Call Signaling & SignalR Control', style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold)),
          const SizedBox(height: 8),
          Text('SignalR Connection Status: ${_isHubConnected ? "Connected" : "Disconnected"}',
              style: TextStyle(color: _isHubConnected ? Colors.green : Colors.red, fontWeight: FontWeight.bold)),
          const SizedBox(height: 12),
          ElevatedButton.icon(
            onPressed: _connectSignalR,
            icon: const Icon(Icons.sync),
            label: const Text('Reconnect SignalR Hub'),
          ),
          const Divider(height: 24),
          const Text('My Connections (Quick Call Launch):', style: TextStyle(fontWeight: FontWeight.bold)),
          const SizedBox(height: 8),
          ListView.builder(
            shrinkWrap: true,
            physics: const NeverScrollableScrollPhysics(),
            itemCount: _connections.length,
            itemBuilder: (context, index) {
              final conn = _connections[index];
              final targetGuidId = conn['connectedUserId'];
              final handle = conn['userId'];

              return ListTile(
                title: Text(handle ?? 'User'),
                subtitle: Text('Guid ID: $targetGuidId'),
                trailing: ElevatedButton(
                  onPressed: () => _initiateCall(targetGuidId, handle),
                  child: const Text('Call Voice'),
                ),
              );
            },
          ),
        ],
      ),
    );
  }

  Widget _buildCallHistoryTab() {
    return Padding(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              const Text('Step 5 & 6: Call History Logs', style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold)),
              IconButton(icon: const Icon(Icons.refresh), onPressed: _fetchCallHistory),
            ],
          ),
          const SizedBox(height: 8),
          Expanded(
            child: _callHistory.isEmpty
                ? const Center(child: Text('No call history records found.'))
                : ListView.builder(
                    itemCount: _callHistory.length,
                    itemBuilder: (context, index) {
                      final item = _callHistory[index];
                      final isOutgoing = item['isOutgoing'] ?? false;
                      final callerUserId = item['callerUserId'];
                      final calleeUserId = item['calleeUserId'];
                      final status = item['status'];
                      final reason = item['missedReason'];
                      final duration = item['durationSeconds'];
                      final startedAt = item['startedAt'];

                      final otherPerson = isOutgoing ? calleeUserId : callerUserId;
                      final directionText = isOutgoing ? 'Outgoing Call -> $otherPerson' : 'Incoming Call <- $otherPerson';

                      return Card(
                        child: ListTile(
                          leading: Icon(
                            status == 'Accepted'
                                ? (isOutgoing ? Icons.call_made : Icons.call_received)
                                : Icons.call_missed,
                            color: status == 'Accepted' ? Colors.green : Colors.red,
                          ),
                          title: Text(directionText),
                          subtitle: Text('Status: $status ${reason != null ? "($reason)" : ""} | Duration: ${duration}s | Time: $startedAt'),
                        ),
                      );
                    },
                  ),
          ),
        ],
      ),
    );
  }

  Widget _buildTrustAndSafetyTab() {
    return SingleChildScrollView(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text('Step 7: Blocked Users List', style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold)),
          const SizedBox(height: 8),
          ElevatedButton(onPressed: _fetchBlockedUsers, child: const Text('Refresh Blocked Users')),
          const SizedBox(height: 8),
          ListView.builder(
            shrinkWrap: true,
            physics: const NeverScrollableScrollPhysics(),
            itemCount: _blockedUsers.length,
            itemBuilder: (context, index) {
              final b = _blockedUsers[index];
              final id = b['blockedUserId'];
              final handle = b['userId'];

              return ListTile(
                title: Text(handle ?? 'Blocked User'),
                subtitle: Text('Guid ID: $id'),
                trailing: ElevatedButton(
                  onPressed: () => _unblockUser(id),
                  style: ElevatedButton.styleFrom(backgroundColor: Colors.orange),
                  child: const Text('Unblock'),
                ),
              );
            },
          ),
          const Divider(height: 32),

          const Text('Step 8: Report User Form', style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold)),
          const SizedBox(height: 8),
          if (_connections.isNotEmpty)
            DropdownButton<String>(
              hint: const Text('Select Connected User to Report'),
              isExpanded: true,
              items: _connections.map<DropdownMenuItem<String>>((c) {
                return DropdownMenuItem<String>(
                  value: c['connectedUserId'],
                  child: Text(c['userId'] ?? c['connectedUserId']),
                );
              }).toList(),
              onChanged: (val) {
                if (val != null) {
                  _reportUser(val);
                }
              },
            ),
          const SizedBox(height: 8),
          TextField(controller: _reportReasonController, decoration: const InputDecoration(labelText: 'Reason (e.g. Harassment, Spam)', border: OutlineInputBorder())),
          const SizedBox(height: 8),
          TextField(controller: _reportNoteController, decoration: const InputDecoration(labelText: 'Note / Details', border: OutlineInputBorder())),
          const Divider(height: 32),

          const Text('Step 9: Account Lifecycle & Soft-Delete', style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold)),
          const SizedBox(height: 8),
          const Text('Soft-deleting your account will flag it as deleted with a 60-day window. Logging back in during the window will silently reactivate it.'),
          const SizedBox(height: 12),
          ElevatedButton.icon(
            onPressed: _softDeleteAccount,
            icon: const Icon(Icons.delete_forever),
            label: const Text('Soft-Delete My Account'),
            style: ElevatedButton.styleFrom(backgroundColor: Colors.red),
          ),
        ],
      ),
    );
  }

  Widget _buildConsoleFooter() {
    return Container(
      height: 160,
      color: Colors.black,
      padding: const EdgeInsets.all(8),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              const Text('Console Audit Log:', style: TextStyle(color: Colors.lightGreenAccent, fontWeight: FontWeight.bold, fontSize: 12)),
              TextButton(
                onPressed: () => setState(() => _consoleLogs.clear()),
                child: const Text('Clear Log', style: TextStyle(fontSize: 10, color: Colors.white54)),
              )
            ],
          ),
          Expanded(
            child: ListView.builder(
              itemCount: _consoleLogs.length,
              itemBuilder: (context, index) {
                return SelectableText(
                  _consoleLogs[index],
                  style: const TextStyle(fontFamily: 'monospace', fontSize: 11, color: Colors.greenAccent),
                );
              },
            ),
          ),
        ],
      ),
    );
  }
}
