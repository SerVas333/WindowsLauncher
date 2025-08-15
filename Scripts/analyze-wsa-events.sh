#!/bin/bash

# ===== WSA Events Analyzer for WindowsLauncher =====
# Analyze WSA integration events and performance metrics
# Usage: ./analyze-wsa-events.sh [--date=YYYY-MM-DD] [--output=file] [--format=json|summary]

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
TARGET_DATE=""
OUTPUT_FILE=""
OUTPUT_FORMAT="summary"
VERBOSE=false

# Parse command line arguments
while [[ $# -gt 0 ]]; do
  case $1 in
    --date=*)
      TARGET_DATE="${1#*=}"
      shift
      ;;
    --output=*)
      OUTPUT_FILE="${1#*=}"
      shift
      ;;
    --format=*)
      OUTPUT_FORMAT="${1#*=}"
      shift
      ;;
    --verbose|-v)
      VERBOSE=true
      shift
      ;;
    --help|-h)
      echo "WSA Events Analyzer for WindowsLauncher"
      echo ""
      echo "Usage: $0 [OPTIONS]"
      echo ""
      echo "Options:"
      echo "  --date=YYYY-MM-DD  Analyze logs for specific date (default: today)"
      echo "  --output=FILE      Save report to file instead of stdout"
      echo "  --format=FORMAT    Output format: summary|json (default: summary)"
      echo "  --verbose, -v      Show detailed analysis"
      echo "  --help, -h         Show this help message"
      echo ""
      echo "Examples:"
      echo "  $0                                    # Analyze today's logs"
      echo "  $0 --date=2024-01-15                # Analyze specific date"
      echo "  $0 --format=json --output=report.json # JSON report to file"
      echo "  $0 --verbose                         # Detailed analysis"
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
log_info() {
    echo -e "${COLOR_GREEN}[INFO]${COLOR_RESET} $1" >&2
}

log_warn() {
    echo -e "${COLOR_YELLOW}[WARN]${COLOR_RESET} $1" >&2
}

log_error() {
    echo -e "${COLOR_RED}[ERROR]${COLOR_RESET} $1" >&2
}

# Get target date
if [ -z "$TARGET_DATE" ]; then
    TARGET_DATE=$(date +%Y-%m-%d)
fi

# Validate date format
if ! date -d "$TARGET_DATE" >/dev/null 2>&1; then
    log_error "Invalid date format: $TARGET_DATE (use YYYY-MM-DD)"
    exit 1
fi

log_info "Analyzing WSA events for date: $TARGET_DATE"

# Find log files for the target date
find_log_files() {
    local pattern="$1"
    find "$LOG_DIR" -name "*${TARGET_DATE}*${pattern}" -type f 2>/dev/null | sort
}

# Get log files
app_logs=$(find_log_files ".log")
wsa_json_logs=$(find_log_files ".json")
debug_logs=$(find_log_files "*debug*.log")

if [ -z "$app_logs" ] && [ -z "$wsa_json_logs" ] && [ -z "$debug_logs" ]; then
    log_error "No log files found for date: $TARGET_DATE"
    log_info "Available log files:"
    ls -la "$LOG_DIR"/ 2>/dev/null || echo "  (none)"
    exit 1
fi

# Analysis functions
analyze_wsa_operations() {
    local log_files="$1"
    local temp_file=$(mktemp)
    
    # Extract WSA-related operations
    if [ -n "$log_files" ]; then
        cat $log_files | grep -i -E "(WSA|Android|Window|Chrome_WidgetWin|ApplicationFrameWindow)" > "$temp_file" 2>/dev/null || true
    fi
    
    if [ ! -s "$temp_file" ]; then
        echo "No WSA operations found"
        rm "$temp_file"
        return
    fi
    
    # Count different types of operations
    local wsa_launches=$(grep -c -i "Launching.*Android.*application" "$temp_file" 2>/dev/null || echo "0")
    local window_found=$(grep -c -i "Found.*WSA.*window" "$temp_file" 2>/dev/null || echo "0")
    local window_cached=$(grep -c -i "Found.*WSA.*window.*cache" "$temp_file" 2>/dev/null || echo "0")
    local window_closed=$(grep -c -i "WSA.*window.*closed" "$temp_file" 2>/dev/null || echo "0")
    local cache_hits=$(grep -c -i "Cached.*WSA.*window" "$temp_file" 2>/dev/null || echo "0")
    local wsa_errors=$(grep -c -i "\[ERROR\].*WSA\|\[ERROR\].*Android" "$temp_file" 2>/dev/null || echo "0")
    
    cat <<EOF
WSA Operations Summary:
  Application Launches: $wsa_launches
  Windows Found: $window_found
  Windows from Cache: $window_cached
  Windows Closed: $window_closed
  Cache Entries Created: $cache_hits
  Errors: $wsa_errors
EOF

    if [ "$VERBOSE" = true ] && [ "$wsa_errors" -gt 0 ]; then
        echo ""
        echo "Recent Errors:"
        grep -i "\[ERROR\].*WSA\|\[ERROR\].*Android" "$temp_file" | tail -5 | while read -r line; do
            echo "  $line"
        done
    fi
    
    rm "$temp_file"
}

analyze_performance() {
    local log_files="$1"
    local temp_file=$(mktemp)
    
    if [ -n "$log_files" ]; then
        # Extract timing information
        cat $log_files | grep -E "launched.*in [0-9]+.*ms|found.*in [0-9]+.*ms" > "$temp_file" 2>/dev/null || true
    fi
    
    if [ ! -s "$temp_file" ]; then
        echo "No performance data found"
        rm "$temp_file"
        return
    fi
    
    # Parse timing data
    local launch_times=$(grep -o -E "launched.*in ([0-9]+).*ms" "$temp_file" | grep -o -E "[0-9]+" || echo "")
    local search_times=$(grep -o -E "found.*in ([0-9]+).*ms" "$temp_file" | grep -o -E "[0-9]+" || echo "")
    
    if [ -n "$launch_times" ]; then
        local avg_launch=$(echo "$launch_times" | awk '{sum+=$1; count++} END {if(count>0) printf "%.1f", sum/count; else print "0"}')
        local max_launch=$(echo "$launch_times" | sort -nr | head -1)
        local min_launch=$(echo "$launch_times" | sort -n | head -1)
        
        echo "Launch Performance:"
        echo "  Average: ${avg_launch}ms"
        echo "  Min: ${min_launch}ms"
        echo "  Max: ${max_launch}ms"
    fi
    
    if [ -n "$search_times" ]; then
        local avg_search=$(echo "$search_times" | awk '{sum+=$1; count++} END {if(count>0) printf "%.1f", sum/count; else print "0"}')
        
        echo "Search Performance:"
        echo "  Average Window Search: ${avg_search}ms"
    fi
    
    rm "$temp_file"
}

analyze_cache_efficiency() {
    local log_files="$1"
    local temp_file=$(mktemp)
    
    if [ -n "$log_files" ]; then
        cat $log_files | grep -i -E "(cache|cached)" > "$temp_file" 2>/dev/null || true
    fi
    
    if [ ! -s "$temp_file" ]; then
        echo "No cache data found"
        rm "$temp_file"
        return
    fi
    
    local cache_hits=$(grep -c -i "Found.*WSA.*window.*cache" "$temp_file" 2>/dev/null || echo "0")
    local cache_misses=$(grep -c -i "cache.*miss\|not.*found.*cache" "$temp_file" 2>/dev/null || echo "0")
    local cache_created=$(grep -c -i "Cached.*WSA.*window" "$temp_file" 2>/dev/null || echo "0")
    local cache_cleared=$(grep -c -i "cache.*clear\|removed.*cache" "$temp_file" 2>/dev/null || echo "0")
    
    local total_requests=$((cache_hits + cache_misses))
    local hit_rate="0"
    if [ "$total_requests" -gt 0 ]; then
        hit_rate=$(echo "$cache_hits $total_requests" | awk '{printf "%.1f", ($1/$2)*100}')
    fi
    
    cat <<EOF
Cache Efficiency:
  Cache Hits: $cache_hits
  Cache Misses: $cache_misses
  Hit Rate: ${hit_rate}%
  Entries Created: $cache_created
  Entries Cleared: $cache_cleared
EOF
    
    rm "$temp_file"
}

analyze_window_lifecycle() {
    local log_files="$1"
    local temp_file=$(mktemp)
    
    if [ -n "$log_files" ]; then
        cat $log_files | grep -i -E "window.*activated|window.*closed|window.*found|window.*monitoring" > "$temp_file" 2>/dev/null || true
    fi
    
    if [ ! -s "$temp_file" ]; then
        echo "No window lifecycle data found"
        rm "$temp_file"
        return
    fi
    
    local windows_found=$(grep -c -i "Found.*WSA.*window" "$temp_file" 2>/dev/null || echo "0")
    local windows_activated=$(grep -c -i "window.*activated\|WindowActivated" "$temp_file" 2>/dev/null || echo "0")
    local windows_closed=$(grep -c -i "window.*closed\|WindowClosed" "$temp_file" 2>/dev/null || echo "0")
    local monitoring_checks=$(grep -c -i "monitoring.*completed\|checked.*instances" "$temp_file" 2>/dev/null || echo "0")
    
    cat <<EOF
Window Lifecycle:
  Windows Found: $windows_found
  Windows Activated: $windows_activated
  Windows Closed: $windows_closed
  Monitoring Checks: $monitoring_checks
EOF
    
    rm "$temp_file"
}

# Generate JSON report
generate_json_report() {
    local analysis_data="$1"
    
    cat <<EOF
{
  "analysis_date": "$TARGET_DATE",
  "generated_at": "$(date -Iseconds)",
  "log_files": {
    "app_logs": [$(echo "$app_logs" | sed 's/.*/"&"/' | paste -sd,)],
    "json_logs": [$(echo "$wsa_json_logs" | sed 's/.*/"&"/' | paste -sd,)],
    "debug_logs": [$(echo "$debug_logs" | sed 's/.*/"&"/' | paste -sd,)]
  },
  "analysis": $analysis_data
}
EOF
}

# Main analysis
analyze_and_output() {
    if [ "$OUTPUT_FORMAT" = "json" ]; then
        echo "{"
        echo '  "date": "'$TARGET_DATE'",'
        echo '  "timestamp": "'$(date -Iseconds)'",'
        echo '  "summary": "WSA Events Analysis"'
        echo "}"
    else
        echo -e "${COLOR_CYAN}===== WSA Events Analysis Report =====${COLOR_RESET}"
        echo -e "${COLOR_GRAY}Date: $TARGET_DATE${COLOR_RESET}"
        echo -e "${COLOR_GRAY}Generated: $(date)${COLOR_RESET}"
        echo ""
        
        echo -e "${COLOR_BLUE}Log Files Analyzed:${COLOR_RESET}"
        [ -n "$app_logs" ] && echo "  Application Logs: $(echo "$app_logs" | wc -l) files"
        [ -n "$wsa_json_logs" ] && echo "  WSA JSON Logs: $(echo "$wsa_json_logs" | wc -l) files"  
        [ -n "$debug_logs" ] && echo "  Debug Logs: $(echo "$debug_logs" | wc -l) files"
        echo ""
        
        echo -e "${COLOR_BLUE}=== WSA Operations ===${COLOR_RESET}"
        analyze_wsa_operations "$app_logs $debug_logs"
        echo ""
        
        echo -e "${COLOR_BLUE}=== Performance Metrics ===${COLOR_RESET}"
        analyze_performance "$app_logs $debug_logs"
        echo ""
        
        echo -e "${COLOR_BLUE}=== Cache Efficiency ===${COLOR_RESET}"
        analyze_cache_efficiency "$app_logs $debug_logs"
        echo ""
        
        echo -e "${COLOR_BLUE}=== Window Lifecycle ===${COLOR_RESET}"
        analyze_window_lifecycle "$app_logs $debug_logs"
        echo ""
        
        if [ "$VERBOSE" = true ]; then
            echo -e "${COLOR_BLUE}=== Recent WSA Events (last 10) ===${COLOR_RESET}"
            if [ -n "$app_logs" ]; then
                cat $app_logs | grep -i -E "(WSA|Android|Window)" | tail -10 | while read -r line; do
                    echo "  $line"
                done
            fi
            echo ""
        fi
        
        echo -e "${COLOR_GREEN}Analysis completed${COLOR_RESET}"
    fi
}

# Execute analysis with proper output redirection
if [ -n "$OUTPUT_FILE" ]; then
    analyze_and_output > "$OUTPUT_FILE"
else
    analyze_and_output
fi

if [ -n "$OUTPUT_FILE" ]; then
    log_info "Report saved to: $OUTPUT_FILE"
fi