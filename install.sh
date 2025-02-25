#!/bin/bash

# Formatting functions
format_setup() {
    # Colors
    export COLOR_RESET='\033[0m'
    export COLOR_BLUE='\033[34m'
    export COLOR_GREEN='\033[32m'
    export COLOR_YELLOW='\033[33m'
    export COLOR_RED='\033[31m'
    
    # Text styles
    export BOLD='\033[1m'
    export ITALIC='\033[3m'
    export UNDERLINE='\033[4m'
}

print_header() {
    echo
    echo -e "${COLOR_BLUE}${BOLD}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${COLOR_RESET}"
    echo -e "${COLOR_BLUE}${BOLD}  $1${COLOR_RESET}"
    echo -e "${COLOR_BLUE}${BOLD}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${COLOR_RESET}"
    echo
}

print_step() {
    echo -e "${COLOR_YELLOW}${BOLD}→${COLOR_RESET} ${ITALIC}$1${COLOR_RESET}"
}

print_success() {
    echo
    echo -e "${COLOR_GREEN}${BOLD}✓${COLOR_RESET} ${BOLD}$1${COLOR_RESET}"
    echo
}

print_warning() {
    echo -e "${COLOR_RED}${BOLD}Warning:${COLOR_RESET} ${ITALIC}$1${COLOR_RESET}"
}

print_command() {
    echo -e "    ${COLOR_BLUE}${BOLD}$1${COLOR_RESET}"
}

# Function to install systemd service
install_systemd_service() {
    print_header "Installing Systemd Service"
    
    # Copy and configure service file
    print_step "Installing service file..."
    SERVICE_PATH="/etc/systemd/system/hophop-rust-server.service"
    INSTALL_PATH="$HOME/HopHopBuildServer"
    
    # Create temporary service file with replacements
    sed -e "s|%USER%|$USER|g" \
        -e "s|%INSTALL_PATH%|$INSTALL_PATH|g" \
        src/hophop/rust_server/systemd/hophop-rust-server.service | sudo tee "$SERVICE_PATH" > /dev/null
    
    # Add sudoers entry for service control
    print_step "Setting up service permissions..."
    SUDOERS_CONTENT="$USER ALL=(ALL) NOPASSWD: /bin/systemctl start hophop-rust-server, /bin/systemctl stop hophop-rust-server, /bin/systemctl restart hophop-rust-server, /bin/systemctl status hophop-rust-server, /bin/systemctl enable hophop-rust-server, /bin/systemctl disable hophop-rust-server, /bin/journalctl -u hophop-rust-server"
    echo "$SUDOERS_CONTENT" | sudo tee "/etc/sudoers.d/hophop-rust-server"
    sudo chmod 440 "/etc/sudoers.d/hophop-rust-server"
    
    # Reload systemd daemon
    print_step "Reloading systemd daemon..."
    sudo systemctl daemon-reload
    
    # Enable the service
    print_step "Enabling service..."
    sudo systemctl enable hophop-rust-server
    
    print_success "Systemd service installed successfully"
}

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --service-only)
            format_setup
            install_systemd_service
            exit 0
            ;;
        *)
            print_warning "Unknown option: $1"
            exit 1
            ;;
    esac
    shift
done

# Check if HopHopBuildServer already exists
if [ -d "$HOME/HopHopBuildServer" ]; then
    print_warning "HopHopBuildServer folder already exists in $HOME"
    echo -e "${ITALIC}Please remove or rename the existing folder before running this script${COLOR_RESET}"
    exit 1
fi

# Function to get user input with timeout
get_input() {
    local prompt="$1"
    local input
    echo -n "$prompt" > /dev/tty
    read -t 30 input < /dev/tty
    echo "$input"
}

# Function to configure environment variables
configure_env() {
    print_header "Server Configuration"
    echo -e "${ITALIC}Would you like to review and modify the default server settings? (y/N)${COLOR_RESET}" > /dev/tty
    response=$(get_input "> ")
    
    if [[ "$response" =~ ^[Yy] ]]; then
        echo "# Custom server settings" > .env.local
        
        while IFS='=' read -r key value; do
            [[ -z "$key" || "$key" =~ ^[[:space:]]*# ]] && continue
            
            key=$(echo "$key" | xargs)
            value=$(echo "$value" | xargs)
            
            echo
            print_step "Setting: ${BOLD}$key${COLOR_RESET}"
            echo -e "${ITALIC}Current value:${COLOR_RESET} ${BOLD}$value${COLOR_RESET}" > /dev/tty
            echo -e "${ITALIC}Would you like to change this value? (y/N)${COLOR_RESET}" > /dev/tty
            change_response=$(get_input "> ")
            
            if [[ "$change_response" =~ ^[Yy] ]]; then
                echo -e "${ITALIC}Enter new value for $key:${COLOR_RESET}" > /dev/tty
                new_value=$(get_input "> ")
                echo "$key=$new_value" >> .env.local
                print_success "Updated $key to: ${BOLD}$new_value${COLOR_RESET}"
            fi
        done < .env
        
        print_success "Configuration complete! Custom settings saved to .env.local"
    else
        print_step "Skipping configuration. Default values will be used."
    fi
}

# Continue with installation if folder doesn't exist
sudo apt install lib32stdc++6 python3-pip python3.12-venv git -y
cd $HOME
git clone https://github.com/nerif-tafu/HopHopBuildServer.git
cd HopHopBuildServer

# Run configuration before starting the server
configure_env

# Create virtual environment and install requirements
python3 -m venv venv
source venv/bin/activate

pip install -e .

# Add format setup at the start of the actual installation
format_setup

# Install systemd service at the end of installation
install_systemd_service

# Update the final installation message
print_header "Installation Complete!"
echo -e "${BOLD}To start the web server, run:${COLOR_RESET}"
echo
print_command "cd $HOME/HopHopBuildServer"
print_command "source venv/bin/activate"
print_command "hophop-web-server"
echo
echo -e "${BOLD}To manage the Rust server, use:${COLOR_RESET}"
echo
print_command "sudo systemctl start hophop-rust-server"
print_command "sudo systemctl stop hophop-rust-server"
print_command "sudo systemctl restart hophop-rust-server"
print_command "sudo systemctl status hophop-rust-server"
echo