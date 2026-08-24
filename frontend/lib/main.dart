import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;

void main() {
  runApp(const ConnectApp());
}

class ConnectApp extends StatelessWidget {
  const ConnectApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Connect - Real-time Calling',
      debugShowCheckedModeBanner: false,
      theme: ThemeData(
        brightness: Brightness.dark,
        scaffoldBackgroundColor: const Color(0xFF0F172A),
        colorScheme: const ColorScheme.dark(
          primary: Color(0xFF6366F1),
          secondary: Color(0xFF06B6D4),
          surface: Color(0xFF1E293B),
          background: Color(0xFF0F172A),
          error: Color(0xFFF43F5E),
        ),
        useMaterial3: true,
      ),
      home: const DashboardScreen(),
    );
  }
}

class DashboardScreen extends StatefulWidget {
  const DashboardScreen({super.key});

  @override
  State<DashboardScreen> createState() => _DashboardScreenState();
}

class _DashboardScreenState extends State<DashboardScreen> {
  final TextEditingController _baseUrlController = TextEditingController(text: 'http://localhost:5200');
  
  bool _isLoading = false;
  String? _status;
  int? _statusCode;
  String? _rawResponse;
  int? _latencyMs;
  DateTime? _lastChecked;
  String? _errorMessage;

  @override
  void initState() {
    super.initState();
    _checkHealth();
  }

  @override
  void dispose() {
    _baseUrlController.dispose();
    super.dispose();
  }

  Future<void> _checkHealth([string? customPath]) async {
    setState(() {
      _isLoading = true;
      _errorMessage = null;
    });

    final stopwatch = Stopwatch()..start();
    final path = customPath ?? '/api/health';
    final url = Uri.parse('${_baseUrlController.text}$path');

    try {
      final response = await http.get(url).timeout(const Duration(seconds: 5));
      stopwatch.stop();

      setState(() {
        _isLoading = false;
        _statusCode = response.statusCode;
        _latencyMs = stopwatch.elapsedMilliseconds;
        _rawResponse = response.body;
        _lastChecked = DateTime.now();

        if (response.statusCode == 200) {
          final data = jsonDecode(response.body);
          _status = data['status'] ?? 'Healthy';
        } else {
          _status = 'Error (${response.statusCode})';
        }
      });
    } catch (e) {
      stopwatch.stop();
      setState(() {
        _isLoading = false;
        _statusCode = 0;
        _latencyMs = stopwatch.elapsedMilliseconds;
        _status = 'Unreachable';
        _errorMessage = e.toString();
        _rawResponse = null;
        _lastChecked = DateTime.now();
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final isOnline = _statusCode == 200;

    return Scaffold(
      body: CustomScrollView(
        slivers: [
          // Header Bar
          SliverAppBar(
            floating: true,
            backgroundColor: const Color(0xFF1E293B).withOpacity(0.9),
            title: Row(
              children: [
                Container(
                  padding: const EdgeInsets.all(8),
                  decoration: BoxDecoration(
                    gradient: const LinearGradient(
                      colors: [Color(0xFF6366F1), Color(0xFF06B6D4)],
                    ),
                    borderRadius: BorderRadius.circular(10),
                  ),
                  child: const Icon(Icons.phone_in_talk, color: Colors.white, size: 24),
                ),
                const SizedBox(width: 12),
                const Text(
                  'Connect',
                  style: TextStyle(fontWeight: FontWeight.bold, fontSize: 22, letterSpacing: 0.5),
                ),
                const SizedBox(width: 8),
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                  decoration: BoxDecoration(
                    color: const Color(0xFF6366F1).withOpacity(0.2),
                    borderRadius: BorderRadius.circular(12),
                    border: Border.all(color: const Color(0xFF6366F1).withOpacity(0.5)),
                  ),
                  child: const Text(
                    'Sprint 0 Local',
                    style: TextStyle(fontSize: 12, color: Color(0xFF818CF8), fontWeight: FontWeight.w600),
                  ),
                ),
              ],
            ),
            actions: [
              Padding(
                padding: const EdgeInsets.only(right: 16),
                child: Row(
                  children: [
                    Container(
                      width: 10,
                      height: 10,
                      decoration: BoxDecoration(
                        shape: BoxShape.circle,
                        color: isOnline ? const Color(0xFF10B981) : const Color(0xFFF43F5E),
                        boxShadow: [
                          BoxShadow(
                            color: isOnline
                                ? const Color(0xFF10B981).withOpacity(0.6)
                                : const Color(0xFFF43F5E).withOpacity(0.6),
                            blurRadius: 8,
                            spreadRadius: 2,
                          )
                        ],
                      ),
                    ),
                    const SizedBox(width: 8),
                    Text(
                      isOnline ? 'API Connected' : 'API Unreachable',
                      style: TextStyle(
                        fontSize: 13,
                        fontWeight: FontWeight.w500,
                        color: isOnline ? const Color(0xFF10B981) : const Color(0xFFF43F5E),
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),

          // Main Content Body
          SliverToBoxAdapter(
            child: Padding(
              padding: const EdgeInsets.all(24.0),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  // Hero Welcome Section
                  _buildHeroCard(),
                  const SizedBox(height: 24),

                  // Health Check Interactive Card
                  _buildHealthCheckCard(),
                  const SizedBox(height: 24),

                  // Exception Handler Tester Card
                  _buildExceptionTesterCard(),
                  const SizedBox(height: 24),

                  // Solution Architecture Overview
                  _buildArchitectureCard(),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildHeroCard() {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(24),
      decoration: BoxDecoration(
        gradient: const LinearGradient(
          colors: [Color(0xFF1E293B), Color(0xFF0F172A)],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: Colors.white.withOpacity(0.08)),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withOpacity(0.2),
            blurRadius: 16,
            offset: const Offset(0, 4),
          ),
        ],
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            '🚀 Connect Local Dev Environment',
            style: TextStyle(fontSize: 24, fontWeight: FontWeight.bold, color: Colors.white),
          ),
          const SizedBox(height: 8),
          Text(
            'ASP.NET Core .NET 8 Clean Architecture API + MediatR CQRS + EF Core SQL Server LocalDB + Flutter Web',
            style: TextStyle(fontSize: 14, color: Colors.slate.shade300, height: 1.4),
          ),
        ],
      ),
    );
  }

  Widget _buildHealthCheckCard() {
    return Container(
      padding: const EdgeInsets.all(24),
      decoration: BoxDecoration(
        color: const Color(0xFF1E293B),
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: Colors.white.withOpacity(0.08)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              const Row(
                children: [
                  Icon(Icons.health_and_safety, color: Color(0xFF06B6D4), size: 24),
                  SizedBox(width: 8),
                  Text(
                    'API Health Check Integration',
                    style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
                  ),
                ],
              ),
              ElevatedButton.icon(
                onPressed: _isLoading ? null : () => _checkHealth(),
                icon: _isLoading
                    ? const SizedBox(
                        width: 16,
                        height: 16,
                        child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white),
                      )
                    : const Icon(Icons.refresh, size: 18),
                label: Text(_isLoading ? 'Checking...' : 'Check API Health'),
                style: ElevatedButton.styleFrom(
                  backgroundColor: const Color(0xFF6366F1),
                  foregroundColor: Colors.white,
                  padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 14),
                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                ),
              ),
            ],
          ),
          const SizedBox(height: 16),
          Row(
            children: [
              Expanded(
                child: TextField(
                  controller: _baseUrlController,
                  decoration: InputDecoration(
                    labelText: 'API Base URL',
                    prefixIcon: const Icon(Icons.link, size: 20),
                    filled: true,
                    fillColor: const Color(0xFF0F172A),
                    border: OutlineInputBorder(
                      borderRadius: BorderRadius.circular(10),
                      borderSide: BorderSide.none,
                    ),
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: 20),
          _buildResponseMetrics(),
        ],
      ),
    );
  }

  Widget _buildResponseMetrics() {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: const Color(0xFF0F172A),
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: Colors.white.withOpacity(0.05)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceAround,
            children: [
              _buildMetricItem('Status', _status ?? 'Not Checked', _statusCode == 200 ? const Color(0xFF10B981) : const Color(0xFFF43F5E)),
              _buildMetricItem('HTTP Code', _statusCode != null ? '$_statusCode' : '-', Colors.white),
              _buildMetricItem('Latency', _latencyMs != null ? '${_latencyMs}ms' : '-', const Color(0xFF06B6D4)),
              _buildMetricItem('Last Checked', _lastChecked != null ? '${_lastChecked!.hour}:${_lastChecked!.minute.toString().padLeft(2, '0')}:${_lastChecked!.second.toString().padLeft(2, '0')}' : '-', Colors.slate.shade400),
            ],
          ),
          if (_rawResponse != null) ...[
            const Divider(height: 24, color: Colors.white10),
            const Text('Response Payload:', style: TextStyle(fontSize: 12, fontWeight: FontWeight.bold, color: Colors.slate)),
            const SizedBox(height: 8),
            Container(
              width: double.infinity,
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(
                color: const Color(0xFF1E293B),
                borderRadius: BorderRadius.circular(8),
              ),
              child: SelectableText(
                _rawResponse!,
                style: const TextStyle(fontFamily: 'monospace', fontSize: 13, color: Color(0xFF34D399)),
              ),
            ),
          ],
          if (_errorMessage != null) ...[
            const Divider(height: 24, color: Colors.white10),
            Text(
              'Error Detail: $_errorMessage',
              style: const TextStyle(color: Color(0xFFF43F5E), fontSize: 13),
            ),
          ]
        ],
      ),
    );
  }

  Widget _buildMetricItem(String label, String value, Color valueColor) {
    return Column(
      children: [
        Text(label, style: TextStyle(fontSize: 12, color: Colors.slate.shade400)),
        const SizedBox(height: 4),
        Text(value, style: TextStyle(fontSize: 15, fontWeight: FontWeight.bold, color: valueColor)),
      ],
    );
  }

  Widget _buildExceptionTesterCard() {
    return Container(
      padding: const EdgeInsets.all(24),
      decoration: BoxDecoration(
        color: const Color(0xFF1E293B),
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: Colors.white.withOpacity(0.08)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Row(
            children: [
              Icon(Icons.bug_report, color: Color(0xFFF43F5E), size: 24),
              SizedBox(width: 8),
              Text(
                'Global Exception Middleware Tester',
                style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
              ),
            ],
          ),
          const SizedBox(height: 8),
          Text(
            'Test API mapping of domain exceptions to ProblemDetails JSON (404, 409, 403, 500)',
            style: TextStyle(fontSize: 13, color: Colors.slate.shade400),
          ),
          const SizedBox(height: 16),
          Wrap(
            spacing: 12,
            runSpacing: 12,
            children: [
              _buildErrorTestButton('Test 404 Not Found', '/api/TestError/not-found', Colors.amber),
              _buildErrorTestButton('Test 409 Conflict', '/api/TestError/conflict', Colors.orange),
              _buildErrorTestButton('Test 403 Forbidden', '/api/TestError/forbidden', Colors.purple),
              _buildErrorTestButton('Test 500 Server Error', '/api/TestError/server-error', Colors.red),
            ],
          ),
        ],
      ),
    );
  }

  Widget _buildErrorTestButton(String label, String endpoint, Color color) {
    return OutlinedButton(
      onPressed: _isLoading ? null : () => _checkHealth(endpoint),
      style: OutlinedButton.styleFrom(
        foregroundColor: color,
        side: BorderSide(color: color.withOpacity(0.6)),
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
      ),
      child: Text(label),
    );
  }

  Widget _buildArchitectureCard() {
    return Container(
      padding: const EdgeInsets.all(24),
      decoration: BoxDecoration(
        color: const Color(0xFF1E293B),
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: Colors.white.withOpacity(0.08)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Row(
            children: [
              Icon(Icons.layers, color: Color(0xFF818CF8), size: 24),
              SizedBox(width: 8),
              Text(
                'Solution Structure Verified',
                style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
              ),
            ],
          ),
          const SizedBox(height: 16),
          _buildArchRow('Connect.Domain', 'Enterprise Enums, Base Entities, Domain Exceptions'),
          _buildArchRow('Connect.Application', 'CQRS MediatR Pipelines, FluentValidation, DTOs'),
          _buildArchRow('Connect.Infrastructure', 'EF Core DbContext, LocalDB Migration, UnitOfWork'),
          _buildArchRow('Connect.Api', 'Serilog Logging, Global Exception Middleware, Swagger UI'),
        ],
      ),
    );
  }

  Widget _buildArchRow(String title, String subtitle) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: Row(
        children: [
          const Icon(Icons.check_circle, color: Color(0xFF10B981), size: 18),
          const SizedBox(width: 12),
          Text(
            title,
            style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 14, color: Colors.white),
          ),
          const SizedBox(width: 12),
          Text(
            '- $subtitle',
            style: TextStyle(fontSize: 13, color: Colors.slate.shade400),
          ),
        ],
      ),
    );
  }
}
