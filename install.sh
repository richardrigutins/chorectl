#!/usr/bin/env sh
# Installs chorectl for the current user, no sudo required.
# Usage: curl -fsSL https://raw.githubusercontent.com/richardrigutins/chorectl/main/install.sh | sh
set -eu

REPO="richardrigutins/chorectl"
BIN_NAME="chorectl"
INSTALL_DIR="${CHORECTL_INSTALL_DIR:-$HOME/.local/bin}"

detect_os() {
	case "$(uname -s)" in
	Linux) echo linux ;;
	Darwin) echo osx ;;
	*)
		echo "chorectl: unsupported OS: $(uname -s)" >&2
		exit 1
		;;
	esac
}

detect_arch() {
	case "$(uname -m)" in
	x86_64 | amd64) echo x64 ;;
	arm64 | aarch64) echo arm64 ;;
	*)
		echo "chorectl: unsupported architecture: $(uname -m)" >&2
		exit 1
		;;
	esac
}

os="$(detect_os)"
arch="$(detect_arch)"
rid="${os}-${arch}"

case "$rid" in
linux-x64 | osx-x64 | osx-arm64) ;;
*)
	echo "chorectl: no build available for $rid" >&2
	exit 1
	;;
esac

asset="chorectl-${rid}.tar.gz"
url="https://github.com/${REPO}/releases/latest/download/${asset}"

tmp_dir="$(mktemp -d)"
trap 'rm -rf "$tmp_dir"' EXIT

echo "Downloading $asset..."
curl -fsSL "$url" -o "$tmp_dir/$asset"
tar -xzf "$tmp_dir/$asset" -C "$tmp_dir"

mkdir -p "$INSTALL_DIR"
mv "$tmp_dir/$BIN_NAME" "$INSTALL_DIR/$BIN_NAME"
chmod +x "$INSTALL_DIR/$BIN_NAME"

echo "Installed chorectl to $INSTALL_DIR/$BIN_NAME"

case ":$PATH:" in
*":$INSTALL_DIR:"*) ;;
*)
	echo ""
	echo "$INSTALL_DIR is not on your PATH. Add this to your shell profile:"
	echo "  export PATH=\"$INSTALL_DIR:\$PATH\""
	;;
esac
