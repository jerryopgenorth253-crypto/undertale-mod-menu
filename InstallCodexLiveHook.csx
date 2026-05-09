using UndertaleModLib.Compiler;
using UndertaleModLib.Models;

EnsureDataLoaded();

UndertaleModLib.Compiler.CodeImportGroup importGroup = new(Data);

UndertaleGameObject objTime = Data.GameObjects.ByName("obj_time");
if (objTime is null)
{
    throw new ScriptException("Could not find obj_time. This does not look like a compatible Undertale data.win.");
}

UndertaleCode createCode = objTime.EventHandlerFor(EventType.Create, (uint)0, Data);
UndertaleCode stepCode = objTime.EventHandlerFor(EventType.Step, (uint)1, Data);
UndertaleCode attackCalcCode = Data.Code.ByName("gml_Script_scr_attackcalc");
UndertaleCode gameStartCode = Data.Code.ByName("gml_Script_SCR_GAMESTART");
UndertaleCode battleCreateCode = Data.Code.ByName("gml_Object_obj_battlecontroller_Create_0");
UndertaleCode battleResetCode = Data.Code.ByName("gml_Object_obj_battlecontroller_Other_5");
if (createCode is null || stepCode is null)
{
    throw new ScriptException("Could not find obj_time Create/Step code entries.");
}
if (attackCalcCode is null)
{
    throw new ScriptException("Could not find scr_attackcalc. This data.win is not compatible with the damage hook.");
}

importGroup.QueueAppend(createCode, @"
codex_live_tick = 0;
codex_live_room_last = -1;
global.codex_live_damage = -1;
global.codex_wasd = 0;
");

if (gameStartCode is not null)
{
    importGroup.QueueAppend(gameStartCode, @"
global.codex_live_damage = -1;
global.codex_wasd = 0;
");
}

importGroup.QueueAppend(stepCode, @"
codex_live_tick += 1;
if (codex_live_tick >= 20)
{
    codex_live_tick = 0;
    if (ossafe_file_exists(""codex_live.ini""))
    {
        ossafe_ini_open(""codex_live.ini"");
        var codex_enabled = ini_read_real(""Live"", ""enabled"", 0);
        if (codex_enabled == 1)
        {
            var codex_lv = ini_read_real(""Player"", ""lv"", -1);
            var codex_hp = ini_read_real(""Player"", ""hp"", -1);
            var codex_xp = ini_read_real(""Player"", ""xp"", -1);
            var codex_gold = ini_read_real(""Player"", ""gold"", -1);
            var codex_damage = ini_read_real(""Player"", ""damage"", -1);
            var codex_room = ini_read_real(""Player"", ""room"", -1);
            var codex_murder = ini_read_real(""Player"", ""murder"", -1);
            var codex_wasd = ini_read_real(""Controls"", ""wasd"", 0);
            global.codex_wasd = codex_wasd;
            if (codex_lv >= 0) global.lv = codex_lv;
            if (codex_hp >= 0)
            {
                global.maxhp = codex_hp;
                global.maxen = codex_hp;
                global.hp = codex_hp;
                global.en = codex_hp;
            }
            if (codex_xp >= 0) global.xp = codex_xp;
            if (codex_gold >= 0) global.gold = codex_gold;
            if (codex_damage >= 0)
            {
                global.codex_live_damage = codex_damage;
                global.at = codex_damage;
                global.wstrength = 0;
                global.pwr = codex_damage;
            }
            if (codex_murder >= 0) global.flag[26] = codex_murder;
            for (var codex_i = 0; codex_i < 8; codex_i += 1)
            {
                var codex_item = ini_read_real(""Items"", ""slot"" + string(codex_i), -1);
                if (codex_item >= 0) global.item[codex_i] = codex_item;
            }
            if (codex_room >= 0 && codex_room != codex_live_room_last)
            {
                codex_live_room_last = codex_room;
                global.currentroom = codex_room;
                room_goto(codex_room);
            }
        }
        ossafe_ini_close();
    }
}
if (variable_global_exists(""codex_wasd"") && global.codex_wasd == 1)
{
    if (global.debug == 0)
    {
        if keyboard_check(ord(""W""))
            keyboard_key_press(vk_up)
        if keyboard_check_released(ord(""W""))
            keyboard_key_release(vk_up)
        if keyboard_check(ord(""A""))
            keyboard_key_press(vk_left)
        if keyboard_check_released(ord(""A""))
            keyboard_key_release(vk_left)
        if keyboard_check(ord(""S""))
            keyboard_key_press(vk_down)
        if keyboard_check_released(ord(""S""))
            keyboard_key_release(vk_down)
        if keyboard_check(ord(""D""))
            keyboard_key_press(vk_right)
        if keyboard_check_released(ord(""D""))
            keyboard_key_release(vk_right)
    }
}
");

string forceDamageGml = @"
if (ossafe_file_exists(""codex_live.ini""))
{
    ossafe_ini_open(""codex_live.ini"");
    var codex_enabled_damage = ini_read_real(""Live"", ""enabled"", 0);
    if (codex_enabled_damage == 1)
    {
        var codex_damage_force = ini_read_real(""Player"", ""damage"", -1);
        if (codex_damage_force >= 0)
        {
            global.codex_live_damage = codex_damage_force;
            global.at = codex_damage_force;
            global.wstrength = 0;
            global.pwr = codex_damage_force;
        }
    }
    ossafe_ini_close();
}
if (variable_global_exists(""codex_live_damage"") && global.codex_live_damage >= 0)
{
    global.at = global.codex_live_damage;
    global.wstrength = 0;
    global.pwr = global.codex_live_damage;
}
";

if (battleCreateCode is not null)
{
    importGroup.QueueAppend(battleCreateCode, forceDamageGml + @"
tempat = global.at;
");
}

if (battleResetCode is not null)
{
    importGroup.QueueAppend(battleResetCode, forceDamageGml);
}

importGroup.QueuePrepend(attackCalcCode, forceDamageGml + @"
if (variable_global_exists(""codex_live_damage"") && global.codex_live_damage >= 0)
{
    damage = global.codex_live_damage;
    exit;
}
");

importGroup.Import();
ScriptMessage("Codex Live Hook v3 installed. Undertale will read codex_live.ini, force DMG during battle startup/attack calculation, and map WASD to movement when enabled.");
