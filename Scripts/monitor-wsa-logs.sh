#!/bin/bash

# ===== WSA Logs Monitor for WindowsLauncher =====
# Real-time monitoring of WSA integration logs from WSL environment
# Usage: ./monitor-wsa-logs.sh [--dev] [--json] [--debug] [--tail-lines=N]

set -euo pipefail

# Configuration
LOG_DIR="/mnt/c/WindowsLauncher/Logs"
COLOR_RESET="\033[0m"
COLOR_RED="\033[31m"
COLOR_GREEN="\033[32m"
COLOR_YELLOW="\033[33m"
COLOR_BLUE="\033[34m"
COLOR_MAGENTA="\033[35m"
COLOR_CYAN="\033[36m"
COLOR_GRAY="\033[90m"

# Default options
DEV_MODE=false
JSON_MODE=false
DEBUG_MODE=false
TAIL_LINES=50
FILTER_WSA=true

# Parse command line arguments
while [[ $# -gt 0 ]]; do
  case $1 in
    --dev)
      DEV_MODE=true
      shift
      ;;
    --json)
      JSON_MODE=true
      shift
      ;;
    --debug)
      DEBUG_MODE=true
      shift
      ;;
    --tail-lines=*)
      TAIL_LINES="${1#*=}"
      shift
      ;;
    --all)
      FILTER_WSA=false
      shift
      ;;
    --help|-h)
      echo "WSA Logs Monitor for WindowsLauncher"
      echo ""
      echo "Usage: $0 [OPTIONS]"
      echo ""
      echo "Options:"
      echo "  --dev              Monitor development logs (more verbose)"
      echo "  --json             Monitor WSA JSON events log"
      echo "  --debug            Monitor debug log (most verbose)"
      echo "  --all              Show all logs, not just WSA-related"
      echo "  --tail-lines=N     Number of lines to show initially (default: 50)"
      echo "  --help, -h         Show this help message"
      echo ""
      echo "Examples:"
      echo "  $0                 # Monitor production WSA logs"
      echo "  $0 --dev          # Monitor development logs"
      echo "  $0 --json         # Monitor WSA JSON events"
      echo "  $0 --debug --all  # Monitor all debug logs"
      exit 0
      ;;
    *)
      echo "Unknown option: $1"
      echo "Use --help for usage information"
      exit 1
      ;;
  esac
done

# Helper functions
print_header() {
    echo -e "${COLOR_CYAN}===== WSA Logs Monitor =====${COLOR_RESET}"
    echo -e "${COLOR_GRAY}Log Directory: $LOG_DIR${COLOR_RESET}"
    echo -e "${COLOR_GRAY}Mode: $([ "$DEV_MODE" = true ] && echo "Development" || echo "Production")${COLOR_RESET}"
    echo -e "${COLOR_GRAY}Filter: $([ "$FILTER_WSA" = true ] && echo "WSA only" || echo "All logs")${COLOR_RESET}"
    echo -e "${COLOR_GRAY}Press Ctrl+C to stop monitoring${COLOR_RESET}"
    echo ""
}

# Colorize log levels
colorize_logs() {
    sed -E \
        -e "s/\[TRACE\]/[${COLOR_GRAY}TRACE${COLOR_RESET}]/g" \
        -e "s/\[DEBUG\]/[${COLOR_BLUE}DEBUG${COLOR_RESET}]/g" \
        -e "s/\[INFO\]/[${COLOR_GREEN}INFO${COLOR_RESET}]/g" \
        -e "s/\[WARN\]/[${COLOR_YELLOW}WARN${COLOR_RESET}]/g" \
        -e "s/\[ERROR\]/[${COLOR_RED}ERROR${COLOR_RESET}]/g" \
        -e "s/\[FATAL\]/[${COLOR_MAGENTA}FATAL${COLOR_RESET}]/g" \
        -e "s/(WSA[^[:space:]]*)/\\${COLOR_CYAN}\\1\\${COLOR_RESET}/g" \
        -e "s/(Android[^[:space:]]*)/\\${COLOR_GREEN}\\1\\${COLOR_RESET}/g" \
        -e "s/(Window[^[:space:]]*)/\\${COLOR_YELLOW}\\1\\${COLOR_RESET}/g" \
        -e "s/(Handle=[0-9A-Fx]+)/\\${COLOR_MAGENTA}\\1\\${COLOR_RESET}/g"
}

# Filter WSA-related logs
filter_wsa() {
    if [ "$FILTER_WSA" = true ]; then
        grep -i -E "(WSA|Android|Window|Chrome_WidgetWin|ApplicationFrameWindow|packageName|Handle=)" || true
    else
        cat
    fi
}

# Get the most recent log file
get_log_file() {
    local pattern="$1"
    find "$LOG_DIR" -name "$pattern" -type f -printf '%T@ %p\n' 2>/dev/null | \
        sort -nr | head -1 | cut -d' ' -f2- || echo ""
}

# Monitor function
monitor_logs() {
    local log_pattern="$1"
    local file_description="$2"
    
    # Check if log directory exists
    if [ ! -d "$LOG_DIR" ]; then
        echo -e "${COLOR_RED}Error: Log directory not found: $LOG_DIR${COLOR_RESET}"
        echo -e "${COLOR_YELLOW}Make sure WindowsLauncher is running and logs are being written${COLOR_RESET}"
        exit 1
    fi

    # Find the most recent log file
    local log_file
    log_file=$(get_log_file "$log_pattern")
    
    if [ -z "$log_file" ] || [ ! -f "$log_file" ]; then
        echo -e "${COLOR_YELLOW}Waiting for $file_description log file...${COLOR_RESET}"
        echo -e "${COLOR_GRAY}Looking for pattern: $log_pattern${COLOR_RESET}"
        
        # Wait for log file to appear
        local wait_count=0
        while [ ! -f "$log_file" ] || [ -z "$log_file" ]; do
            sleep 2
            log_file=$(get_log_file "$log_pattern")
            wait_count=$((wait_count + 1))
            
            if [ $wait_count -gt 30 ]; then
                echo -e "${COLOR_RED}Timeout waiting for log file. Is WindowsLauncher running?${COLOR_RESET}"
                exit 1
            fi
        done
    fi
    
    echo -e "${COLOR_GREEN}Monitoring: $log_file${COLOR_RESET}"
    echo ""
    
    # Show initial lines
    if [ -f "$log_file" ]; then
        echo -e "${COLOR_GRAY}=== Last $TAIL_LINES lines ===${COLOR_RESET}"
        tail -n "$TAIL_LINES" "$log_file" | filter_wsa | colorize_logs
        echo -e "${COLOR_GRAY}=== Live monitoring (new entries) ===${COLOR_RESET}"
    fi
    
    # Start monitoring
    tail -f "$log_file" | filter_wsa | colorize_logs
}

# Special JSON monitoring
monitor_json() {
    local log_pattern="$1"
    local file_description="$2"
    
    local log_file
    log_file=$(get_log_file "$log_pattern")
    
    if [ -z "$log_file" ] || [ ! -f "$log_file" ]; then
        echo -e "${COLOR_YELLOW}Waiting for $file_description log file...${COLOR_RESET}"
        while [ ! -f "$log_file" ] || [ -z "$log_file" ]; do
            sleep 2
            log_file=$(get_log_file "$log_pattern")
        done
    fi
    
    echo -e "${COLOR_GREEN}Monitoring JSON events: $log_file${COLOR_RESET}"
    echo ""
    
    # Monitor JSON log with pretty printing
    tail -f "$log_file" | while IFS= read -r line; do
        if command -v jq >/dev/null 2>&1; then
            echo "$line" | jq -C '.' 2>/dev/null || echo "$line"
        else
            echo "$line" | python3 -m json.tool 2>/dev/null || echo "$line"
        fi
    done
}

# Main execution
print_header

# Determine which log to monitor based on options
if [ "$JSON_MODE" = true ]; then
    if [ "$DEV_MODE" = true ]; then
        monitor_json "wsa-dev-*.json" "WSA JSON Development"
    else
        monitor_json "wsa-events-*.json" "WSA JSON Production"
    fi
elif [ "$DEBUG_MODE" = true ]; then
    if [ "$DEV_MODE" = true ]; then
        monitor_logs "debug-dev-*.log" "Debug Development"
    else
        monitor_logs "debug-*.log" "Debug Production"
    fi
else
    if [ "$DEV_MODE" = true ]; then
        monitor_logs "app-dev-*.log" "Application Development"
    else
        monitor_logs "app-*.log" "Application Production"
    fi
fi