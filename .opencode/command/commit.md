---
description: Commit en improvements, merge a main, push de main y vuelta a improvements.
agent: build
---

Ejecuta este flujo git en orden. Tras cada paso comprueba que ha tenido éxito antes de continuar; si falla, detente y reporta el error:

1. Si `git status --porcelain` no está vacío, ejecuta `git add -A` y `git commit -m "<mensaje>"`. El mensaje es lo que el usuario escribió tras `/commit`; si no escribió nada, usa "WIP". Si el árbol está limpio, salta este paso.
2. `git checkout main` (verifica que la rama cambió).
3. `git merge --no-ff improvements -m "Merge branch 'improvements'"`. Si hay conflictos o falla, ejecuta `git merge --abort`, vuelve a `git checkout improvements` y reporta el error sin hacer push.
4. `git push origin main`. Si falla (p. ej. el remoto tiene commits nuevos), NO fuerces el push; vuelve a `git checkout improvements` y reporta el error.
5. `git checkout improvements`.
6. Resume el resultado: últimos commits de `main` y de `improvements`, confirmación de que estás de vuelta en `improvements` y estado del árbol de trabajo.