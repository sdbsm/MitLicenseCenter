#!/usr/bin/env node
// Идемпотентно настраивает Git hooks-каталог на ../.husky.
// Запускается через `pnpm install` (скрипт prepare). В CI пропускается.
import { execSync } from "node:child_process";

if (process.env.CI === "true") {
  process.exit(0);
}

try {
  execSync("git rev-parse --git-dir", { stdio: "ignore" });
} catch {
  // Not a git checkout (npm pack, fresh tarball install, etc.) — silently skip.
  process.exit(0);
}

try {
  const current = execSync("git config --get core.hooksPath", { encoding: "utf8" }).trim();
  if (current === ".husky") {
    process.exit(0);
  }
} catch {
  // No value set — fall through and set it.
}

execSync("git -C .. config core.hooksPath .husky", { stdio: "inherit" });
console.log("[git-hooks] core.hooksPath = .husky");
