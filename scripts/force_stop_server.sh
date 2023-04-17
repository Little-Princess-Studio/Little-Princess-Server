#!/bin/bash
sudo -S pkill -9 LPS || echo "Process was not running."
sudo -S pkill -9 dotnet watch run || echo "Process was not running."