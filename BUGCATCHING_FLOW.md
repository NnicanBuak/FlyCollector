# Bug Catching Flow Documentation

## Механика ловли жуков (обновлено)

### Поток выполнения:

1. **Игрок кликает ЛКМ на жуке**
   - Жук входит в режим Inspect (летит к камере)
   - AI жука отключается
   - Показывается CollectHintUI

2. **Игрок нажимает ПКМ (Правая кнопка мыши)**
   - Вызывается банка из пула (`BugJarPool`)
   - **targetBug устанавливается на банку** (`activeJar.SetTargetBug(go)`)
   - Банка летит на стол (`activeJar.FlyToTable()`)
   - **Жук перемещается на стол** (к `bugTablePosition`)
   - **Инспекция завершается**
   - Жук остается на столе:
     - ✅ AI отключен (`DisableAI(true)`)
     - ✅ Rigidbody кинематический (`isKinematic = true`)
     - ✅ Colliders включены
   - Банка на столе в состоянии `AtTable`

3. **Игрок кликает ЛКМ на банку**
   - `InteractableObject` на банке вызывает `SealBugAction`
   - `SealBugAction` проверяет:
     - ✅ Банка в состоянии `AtTable`
     - ✅ У банки есть `targetBug`
   - Вызывается `jarTrap.Seal()`
   - В `SealCoroutine()`:
     - Жук добавляется в инвентарь
     - Жук регистрируется как пойманный (`CaughtBugsRuntime`)
     - Счетчик банок уменьшается (`BugCounter.DecrementJars()`)
     - **Жук удаляется со сцены** (`Destroy(targetBug)`)
     - Банка возвращается в исходное положение
     - Банка возвращается в пул

### Критические моменты:

- **bugTablePosition** должен быть настроен у каждого жука:
  - Вариант 1: Child Transform с именем "TablePosition"
  - Вариант 2: Поле `tablePosition` в компоненте на жуке
  - Вариант 3: Свойство `TablePosition` в компоненте на жуке

- **BugJarTrap** должен иметь:
  - `tablePosition` - куда летит банка
  - `InteractableObject` с `SealBugAction`

### Альтернативный поток (ESC или обычный выход):

Если игрок нажимает ESC вместо ПКМ:
- Жук возвращается в исходное положение
- AI включается обратно
- Rigidbody становится не-кинематическим
- Жук продолжает свое поведение

## Файлы системы:

### Core:
- `InspectSession.cs` - управление режимом inspect
- `BugJarTrap.cs` - поведение банки (лететь/запечатать)
- `BugJarPool.cs` - пул банок
- `SealBugAction.cs` - action для запечатывания

### Supporting:
- `BugCounter.cs` - счетчик доступных банок
- `BugAI.cs` - AI жука (DisableAI)
- `CaughtBugsRuntime.cs` - регистрация пойманных
- `InventoryManager.cs` - добавление в инвентарь

## Debug:

Включите `showDebug` в следующих компонентах:
- `BugJarTrap` - видеть состояния банки
- `BugJarPool` - видеть пул банок
- `SealBugAction` - видеть процесс запечатывания

Логи при правильной работе:
```
[InspectSession] Target bug '...' set on jar. Flying jar to table...
[InspectSession] Bug '...' placed on table, waiting for jar to seal.
[BugJarTrap] Arrived at table
[SealBugAction] Sealing jar with bug: ...
[BugJarTrap] Seal complete, processing bug collection...
[BugJarTrap] Added ... to inventory
```
