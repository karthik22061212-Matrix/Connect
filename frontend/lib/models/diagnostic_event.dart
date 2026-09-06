class DiagnosticEvent {
  final String id;
  final String timestamp;
  final String severity;
  final String component;
  final String eventName;
  final String message;
  final String? userId;
  final String? sessionId;
  final String? correlationId;
  final String? callId;
  final Map<String, dynamic>? metadata;

  DiagnosticEvent({
    required this.id,
    required this.timestamp,
    required this.severity,
    required this.component,
    required this.eventName,
    required this.message,
    this.userId,
    this.sessionId,
    this.correlationId,
    this.callId,
    this.metadata,
  });

  Map<String, dynamic> toJson() {
    return {
      'id': id,
      'timestamp': timestamp,
      'severity': severity,
      'component': component,
      'eventName': eventName,
      'message': message,
      if (userId != null) 'userId': userId,
      if (sessionId != null) 'sessionId': sessionId,
      if (correlationId != null) 'correlationId': correlationId,
      if (callId != null) 'callId': callId,
      if (metadata != null) 'metadata': metadata,
    };
  }

  factory DiagnosticEvent.fromJson(Map<String, dynamic> json) {
    return DiagnosticEvent(
      id: json['id'],
      timestamp: json['timestamp'],
      severity: json['severity'],
      component: json['component'],
      eventName: json['eventName'],
      message: json['message'],
      userId: json['userId'],
      sessionId: json['sessionId'],
      correlationId: json['correlationId'],
      callId: json['callId'],
      metadata: json['metadata'],
    );
  }
}
