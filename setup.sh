#!/usr/bin/env bash
set -e

if [ -z "$1" ]; then
  echo "Usage: ./setup.sh <remote-repo-url>"
  exit 1
fi

REMOTE="$1"

if [ ! -d .git ]; then
  git init
  echo "Initialized empty Git repository"
else
  echo "Git repository already initialized"
fi

if command -v git-lfs >/dev/null 2>&1; then
  git lfs install --skip-repo
  echo "Git LFS installed"
else
  echo "WARNING: git-lfs not found. Install git-lfs if you plan to track large binary files."
fi

git add .
git commit -m "Initial Unity project commit" || true
git remote add origin "$REMOTE" || true
git branch -M main || true
git push -u origin main

git checkout -b develop || true
git push -u origin develop

echo "Setup complete for remote: $REMOTE"
