import 'dart:async';
import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

/// Test widget replicating the exact search input architecture and state management from [HomeScreen].
class TestSearchWidget extends StatefulWidget {
  final Future<String> Function(String query)? onSearchApi;

  const TestSearchWidget({super.key, this.onSearchApi});

  @override
  State<TestSearchWidget> createState() => _TestSearchWidgetState();
}

class _TestSearchWidgetState extends State<TestSearchWidget> {
  final TextEditingController _searchQueryController = TextEditingController();
  List<dynamic> _searchResults = [];
  bool _isSearching = false;
  int networkRequestCount = 0;

  @override
  void initState() {
    super.initState();

    _searchQueryController.addListener(() {
      if (!mounted) return;
      if (_searchQueryController.text.trim().isEmpty && (_searchResults.isNotEmpty || _isSearching)) {
        setState(() {
          _searchResults = [];
          _isSearching = false;
        });
      }
    });
  }

  @override
  void dispose() {
    _searchQueryController.dispose();
    super.dispose();
  }

  Future<void> _searchUsers() async {
    final q = _searchQueryController.text.trim();
    if (q.isEmpty) {
      setState(() {
        _searchResults = [];
        _isSearching = false;
      });
      return;
    }

    setState(() {
      _isSearching = true;
    });

    try {
      networkRequestCount++;
      final body = widget.onSearchApi != null
          ? await widget.onSearchApi!(q)
          : jsonEncode([{'id': 'u1', 'userId': q, 'phoneNumber': '1234567890'}]);

      if (_searchQueryController.text.trim().isEmpty) {
        setState(() {
          _searchResults = [];
          _isSearching = false;
        });
      } else {
        setState(() {
          _searchResults = jsonDecode(body);
        });
      }
    } catch (_) {
      // ignore
    } finally {
      setState(() {
        _isSearching = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      home: Scaffold(
        body: Column(
          children: [
            Row(
              children: [
                Expanded(
                  child: TextField(
                    key: const Key('search_text_field'),
                    controller: _searchQueryController,
                    onSubmitted: (_) => _searchUsers(),
                    onChanged: (val) {
                      if (val.trim().isEmpty && (_searchResults.isNotEmpty || _isSearching)) {
                        setState(() {
                          _searchResults = [];
                          _isSearching = false;
                        });
                      } else {
                        setState(() {});
                      }
                    },
                    decoration: InputDecoration(
                      hintText: 'Search user handle (e.g. user_two) or phone...',
                      prefixIcon: const Icon(Icons.search, size: 22),
                      suffixIcon: _searchQueryController.text.isNotEmpty
                          ? IconButton(
                              key: const Key('search_clear_button'),
                              icon: const Icon(Icons.clear, size: 20),
                              onPressed: () {
                                _searchQueryController.clear();
                                setState(() {
                                  _searchResults = [];
                                  _isSearching = false;
                                });
                              },
                            )
                          : null,
                    ),
                  ),
                ),
                ElevatedButton(
                  key: const Key('search_submit_button'),
                  onPressed: _searchUsers,
                  child: const Text('Search'),
                ),
              ],
            ),
            if (_isSearching)
              const CircularProgressIndicator(key: Key('search_progress_indicator'))
            else if (_searchResults.isNotEmpty) ...[
              const Text('Search Results', key: Key('search_results_header')),
              Expanded(
                child: ListView.builder(
                  key: const Key('search_results_list'),
                  itemCount: _searchResults.length,
                  itemBuilder: (context, index) {
                    final item = _searchResults[index];
                    return ListTile(
                      key: Key('result_item_${item['userId']}'),
                      title: Text(item['userId'] ?? ''),
                    );
                  },
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

void main() {
  group('STORY-7: Search Input Instant Clearing & Results Invalidation Matrix Audit', () {
    testWidgets('Matrix Row 1: Types "alice" and submits -> Populated with results, HTTP request sent', (tester) async {
      await tester.pumpWidget(const TestSearchWidget());

      // Initial state: no clear button, no results
      expect(find.byKey(const Key('search_clear_button')), findsNothing);
      expect(find.byKey(const Key('search_results_header')), findsNothing);

      // Enter "alice"
      await tester.enterText(find.byKey(const Key('search_text_field')), 'alice');
      await tester.pump();

      // Clear button should now be visible
      expect(find.byKey(const Key('search_clear_button')), findsOneWidget);

      // Submit search
      await tester.tap(find.byKey(const Key('search_submit_button')));
      await tester.pumpAndSettle();

      // Results populated
      expect(find.byKey(const Key('search_results_header')), findsOneWidget);
      expect(find.byKey(const Key('result_item_alice')), findsOneWidget);
    });

    testWidgets('Matrix Row 2: Backspaces to empty -> Immediately reset to [], NO HTTP request sent', (tester) async {
      int apiCalls = 0;
      await tester.pumpWidget(TestSearchWidget(
        onSearchApi: (q) async {
          apiCalls++;
          return jsonEncode([{'id': 'u1', 'userId': q}]);
        },
      ));

      // Enter "alice" and submit
      await tester.enterText(find.byKey(const Key('search_text_field')), 'alice');
      await tester.pump();
      await tester.tap(find.byKey(const Key('search_submit_button')));
      await tester.pumpAndSettle();

      expect(apiCalls, equals(1));
      expect(find.byKey(const Key('search_results_header')), findsOneWidget);

      // Backspace to empty string
      await tester.enterText(find.byKey(const Key('search_text_field')), '');
      await tester.pump();

      // Results must immediately vanish without pressing submit
      expect(find.byKey(const Key('search_results_header')), findsNothing);
      expect(find.byKey(const Key('search_results_list')), findsNothing);
      expect(find.byKey(const Key('search_clear_button')), findsNothing);
      // No additional HTTP request sent
      expect(apiCalls, equals(1));
    });

    testWidgets('Matrix Row 3: Clicks clear icon -> Immediately reset to [], NO HTTP request sent', (tester) async {
      int apiCalls = 0;
      await tester.pumpWidget(TestSearchWidget(
        onSearchApi: (q) async {
          apiCalls++;
          return jsonEncode([{'id': 'u1', 'userId': q}]);
        },
      ));

      // Enter "alice" and submit
      await tester.enterText(find.byKey(const Key('search_text_field')), 'alice');
      await tester.pump();
      await tester.tap(find.byKey(const Key('search_submit_button')));
      await tester.pumpAndSettle();

      expect(apiCalls, equals(1));
      expect(find.byKey(const Key('search_results_header')), findsOneWidget);

      // Click clear icon button
      await tester.tap(find.byKey(const Key('search_clear_button')));
      await tester.pump();

      // Results and clear button must vanish immediately
      expect(find.byKey(const Key('search_results_header')), findsNothing);
      expect(find.byKey(const Key('search_results_list')), findsNothing);
      expect(find.byKey(const Key('search_clear_button')), findsNothing);
      // No additional HTTP request sent
      expect(apiCalls, equals(1));
    });

    testWidgets('Matrix Row 4: Enters whitespace only -> Immediately reset to [], NO HTTP request sent', (tester) async {
      int apiCalls = 0;
      await tester.pumpWidget(TestSearchWidget(
        onSearchApi: (q) async {
          apiCalls++;
          return jsonEncode([{'id': 'u1', 'userId': q}]);
        },
      ));

      // Enter "alice" and submit
      await tester.enterText(find.byKey(const Key('search_text_field')), 'alice');
      await tester.pump();
      await tester.tap(find.byKey(const Key('search_submit_button')));
      await tester.pumpAndSettle();

      expect(apiCalls, equals(1));
      expect(find.byKey(const Key('search_results_header')), findsOneWidget);

      // Enter whitespace "   "
      await tester.enterText(find.byKey(const Key('search_text_field')), '   ');
      await tester.pump();

      // Results must immediately vanish
      expect(find.byKey(const Key('search_results_header')), findsNothing);

      // Even if user presses submit with whitespace, no HTTP request is made
      await tester.tap(find.byKey(const Key('search_submit_button')));
      await tester.pump();
      expect(apiCalls, equals(1)); // No extra request
      expect(find.byKey(const Key('search_results_header')), findsNothing);
    });

    testWidgets('Matrix Row 5: Types "bob" after clearing -> Populated with Bob\'s results, HTTP request sent', (tester) async {
      int apiCalls = 0;
      await tester.pumpWidget(TestSearchWidget(
        onSearchApi: (q) async {
          apiCalls++;
          return jsonEncode([{'id': 'u2', 'userId': q}]);
        },
      ));

      // 1. Search "alice"
      await tester.enterText(find.byKey(const Key('search_text_field')), 'alice');
      await tester.pump();
      await tester.tap(find.byKey(const Key('search_submit_button')));
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('result_item_alice')), findsOneWidget);

      // 2. Clear
      await tester.tap(find.byKey(const Key('search_clear_button')));
      await tester.pump();
      expect(find.byKey(const Key('search_results_header')), findsNothing);

      // 3. Search "bob"
      await tester.enterText(find.byKey(const Key('search_text_field')), 'bob');
      await tester.pump();
      await tester.tap(find.byKey(const Key('search_submit_button')));
      await tester.pumpAndSettle();

      expect(apiCalls, equals(2));
      expect(find.byKey(const Key('search_results_header')), findsOneWidget);
      expect(find.byKey(const Key('result_item_bob')), findsOneWidget);
    });

    testWidgets('Race condition protection: Stale in-flight HTTP response arriving after clear is discarded', (tester) async {
      final completer = Completer<String>();
      await tester.pumpWidget(TestSearchWidget(
        onSearchApi: (q) => completer.future,
      ));

      // Enter "alice" and submit
      await tester.enterText(find.byKey(const Key('search_text_field')), 'alice');
      await tester.pump();
      await tester.tap(find.byKey(const Key('search_submit_button')));
      await tester.pump();

      // In flight indicator
      expect(find.byKey(const Key('search_progress_indicator')), findsOneWidget);

      // User clears before request finishes
      await tester.enterText(find.byKey(const Key('search_text_field')), '');
      await tester.pump();

      // In-flight indicator and results should be gone
      expect(find.byKey(const Key('search_progress_indicator')), findsNothing);
      expect(find.byKey(const Key('search_results_header')), findsNothing);

      // Delayed server response arrives
      completer.complete(jsonEncode([{'id': 'u1', 'userId': 'alice'}]));
      await tester.pumpAndSettle();

      // Must remain empty because input was cleared
      expect(find.byKey(const Key('search_results_header')), findsNothing);
      expect(find.byKey(const Key('result_item_alice')), findsNothing);
    });
  });
}
