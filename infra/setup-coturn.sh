#!/bin/bash
# ==============================================================================
# Coturn TURN Server Installation & Configuration Script for Ubuntu 22.04 LTS
# Project: Connect (Sprint 7.5)
# ==============================================================================

set -e

TURN_USER=${1:-"connectturn"}
TURN_PASS=${2:-"ConnectTurn2026SecurePass!"}
REALM=${3:-"connect.azurewebsites.net"}

echo "=== Updating package repositories and installing coturn ==="
sudo apt-get update -y
sudo apt-get install -y coturn

echo "=== Enabling coturn daemon ==="
sudo sed -i 's/TURNSERVER_ENABLED=0/TURNSERVER_ENABLED=1/' /etc/default/coturn

# Get public IP address of VM
PUBLIC_IP=$(curl -s ifconfig.me)
echo "Detected Public IP: $PUBLIC_IP"

echo "=== Writing /etc/turnserver.conf ==="
sudo cat <<EOF > /etc/turnserver.conf
# Coturn Configuration for Connect WebRTC
listening-port=3478
tls-listening-port=5349

listening-ip=0.0.0.0
external-ip=$PUBLIC_IP

realm=$REALM
user=$TURN_USER:$TURN_PASS

# WebRTC media relay port range
min-port=49152
max-port=65535

# Security settings
fingerprint
lt-cred-mech
no-cli
no-loopback-peers
no-multicast-peers

# Logging
log-file=/var/log/turnserver/turnserver.log
verbose
EOF

echo "=== Restarting coturn service ==="
sudo systemctl restart coturn
sudo systemctl enable coturn

echo "=== Coturn status ==="
sudo systemctl status coturn --no-pager

echo "=== Coturn TURN Server configured successfully ==="
echo "TURN URL: turn:$PUBLIC_IP:3478"
echo "Username: $TURN_USER"
