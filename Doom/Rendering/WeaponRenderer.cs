using Doom.Configuration;
using Doom.Gameplay;

namespace Doom.Rendering;

/// <summary>
///     Отрисовка оружия от первого лица: покачивание при ходьбе, отдача, подъём ствола
///     после смены и вспышка выстрела. Каждый ствол рисуется своей процедурной графикой.
/// </summary>
internal static class WeaponRenderer
{
    private const double ScaleBaseRows = 30.0;
    private const double BobSpeed = 4.5;
    private const double BobAmplitude = 2.0;

    public static void Draw(Framebuffer framebuffer, Viewport viewport, Player player, double nowSeconds)
    {
        double scale = Math.Max(1.0, viewport.Rows / ScaleBaseRows);
        WeaponInfo info = WeaponCatalog.Get(player.CurrentWeapon);

        // Оружие прижато к низу: по вертикали анимируются отдача и подъём после смены.
        int bottom = viewport.Rows - 2;
        int bobX = (int)Math.Round(
            Math.Sin(player.WeaponBobTime * BobSpeed) * BobAmplitude * scale * player.BobIntensity);

        int recoil = Recoil(info, player, nowSeconds, scale);
        int raise = (int)Math.Round((1.0 - player.RaiseProgress(nowSeconds)) * GameConfig.WeaponRaiseOffset * scale);
        bool muzzleFlash = nowSeconds < player.MuzzleFlashUntil;
        double fireProgress = FireProgress(info, player, nowSeconds);
        double thrust = Math.Sin(Math.PI * fireProgress);

        int centerX = viewport.Columns / 2 + bobX;
        int baseY = bottom + recoil + raise;

        switch (player.CurrentWeapon)
        {
            case WeaponKind.Fist:
                DrawFist(framebuffer, viewport, player, centerX, baseY, thrust, scale);
                break;
            case WeaponKind.Chainsaw:
                DrawChainsaw(framebuffer, viewport, player, centerX, baseY, thrust, scale);
                break;
            case WeaponKind.Pistol:
                DrawPistol(framebuffer, viewport, centerX, baseY, muzzleFlash, scale);
                break;
            case WeaponKind.Shotgun:
                DrawShotgun(framebuffer, viewport, centerX, baseY, muzzleFlash, scale);
                break;
            case WeaponKind.Chaingun:
                DrawChaingun(framebuffer, viewport, player, centerX, baseY, muzzleFlash, scale);
                break;
            case WeaponKind.RocketLauncher:
                DrawRocketLauncher(framebuffer, viewport, centerX, baseY, muzzleFlash, scale);
                break;
            case WeaponKind.PlasmaRifle:
                DrawPlasmaRifle(framebuffer, viewport, player, centerX, baseY, muzzleFlash, nowSeconds, scale);
                break;
            case WeaponKind.Bfg9000:
                DrawBfg(framebuffer, viewport, centerX, baseY, muzzleFlash, fireProgress, scale);
                break;
        }
    }

    /// <summary>Отдача: ствол на мгновение уходит вниз, затем возвращается на место.</summary>
    private static int Recoil(WeaponInfo info, Player player, double nowSeconds, double scale)
    {
        double sinceFire = nowSeconds - player.LastFireTime;
        if (sinceFire < 0 || sinceFire >= GameConfig.RecoilSeconds || info.Recoil <= 0)
            return 0;

        double k = 1.0 - sinceFire / GameConfig.RecoilSeconds;
        return -(int)Math.Round(k * info.Recoil * scale);
    }

    /// <summary>Прогресс анимации выстрела: 0 — сразу после выстрела, 1 — анимация закончилась.</summary>
    private static double FireProgress(WeaponInfo info, Player player, double nowSeconds)
    {
        double sinceFire = nowSeconds - player.LastFireTime;
        if (sinceFire < 0)
            return 1.0;

        double duration = Math.Min(info.Cooldown, GameConfig.WeaponAnimationSeconds);
        return duration <= 0 ? 1.0 : Math.Min(1.0, sinceFire / duration);
    }


    private static void DrawPistol(
        Framebuffer framebuffer, Viewport viewport, int centerX, int baseY, bool muzzleFlash, double scale)
    {
        int bodyWidth = Size(10, scale);
        int bodyHeight = Size(8, scale);
        int left = centerX - bodyWidth / 2;
        int top = baseY - bodyHeight + 1;

        // Корпус уходит за нижний край игровой области — оружие «прижато» к низу.
        int extendedHeight = viewport.Rows - 1 - top;
        if (extendedHeight > 0)
            framebuffer.PaintRect(left, top, bodyWidth, extendedHeight, 0x3A3A42);

        // Блик по центру — только в верхней части.
        framebuffer.PaintRect(left + bodyWidth / 2 - Size(1, scale), top, Size(2, scale), bodyHeight, 0x6E6E7A);

        // Ствол торчит сверху.
        framebuffer.PaintRect(centerX - Size(1, scale), top - Size(2, scale), Size(2, scale), Size(2, scale), 0x222228);

        if (!muzzleFlash)
            return;

        framebuffer.PaintRect(centerX - Size(2, scale), top - Size(4, scale), Size(4, scale), Size(2, scale), 0xFFF070);
        framebuffer.PaintRect(centerX - Size(1, scale), top - Size(5, scale), Size(2, scale), Size(1, scale), 0xFFFFFF);
    }

    /// <summary>Кулак: попеременно бьёт слева и справа, в конце удара втягивается обратно.</summary>
    private static void DrawFist(
        Framebuffer framebuffer, Viewport viewport, Player player, int centerX, int baseY, double thrust, double scale)
    {
        int size = Size(9, scale);
        int side = player.SwingFromLeft ? -1 : 1;
        int outward = Size(7, scale);
        int shift = (int)Math.Round(thrust * outward);

        int left = centerX + side * (Size(5, scale) + outward - shift) - size / 2;
        int top = baseY - size + 1 - (int)Math.Round(thrust * Size(4, scale));

        int height = viewport.Rows - 1 - top;
        if (height <= 0)
            return;

        framebuffer.PaintRect(left, top, size, height, WeaponPalette.Skin);
        framebuffer.PaintRect(left, top, size, Size(2, scale), WeaponPalette.SkinShade);

        // Тень по внешнему краю кулака и между костяшками.
        int shadowLeft = side < 0 ? left : left + size - Size(2, scale);
        framebuffer.PaintRect(shadowLeft, top, Size(2, scale), height, WeaponPalette.SkinShade);
        framebuffer.PaintRect(left + size / 2, top + Size(2, scale), Size(1, scale), height, WeaponPalette.SkinShade);
    }

    /// <summary>Бензопила: оранжевый корпус сбоку и полотно с зубьями, дёргающимися при вращении.</summary>
    private static void DrawChainsaw(
        Framebuffer framebuffer, Viewport viewport, Player player, int centerX, int baseY, double thrust, double scale)
    {
        int bodyWidth = Size(15, scale);
        int bodyHeight = Size(9, scale);
        int left = centerX - Size(4, scale) - (int)Math.Round(thrust * Size(6, scale));
        int top = baseY - bodyHeight + 1 - (int)Math.Round(thrust * Size(3, scale));

        int height = viewport.Rows - 1 - top;
        if (height <= 0)
            return;

        framebuffer.PaintRect(left, top, bodyWidth, height, WeaponPalette.SawBody);
        framebuffer.PaintRect(left + Size(2, scale), top + Size(1, scale), bodyWidth - Size(4, scale), height,
            WeaponPalette.SawGrip);
        framebuffer.PaintRect(left, top, bodyWidth, Size(1, scale), WeaponPalette.SawTeeth);

        // Полотно уходит вверх и к центру экрана.
        int bladeWidth = Size(3, scale);
        int bladeHeight = Size(13, scale);
        int bladeLeft = left + bodyWidth - Size(2, scale);
        int bladeTop = top - bladeHeight + Size(3, scale);

        framebuffer.PaintRect(bladeLeft, bladeTop, bladeWidth, bladeHeight, WeaponPalette.SawBlade);

        bool spinning = thrust > 0.02;

        for (int index = 0; index < 4; index++)
        {
            int toothY = bladeTop + Size(1, scale) + index * Size(3, scale);
            if (spinning)
                toothY += (int)Math.Round(Math.Sin(player.SpinPhase + index * 1.7) * scale * 0.8);

            framebuffer.PaintRect(bladeLeft - Size(1, scale), toothY, Size(1, scale), Size(1, scale),
                WeaponPalette.SawTeeth);
            framebuffer.PaintRect(bladeLeft + bladeWidth, toothY + Size(1, scale), Size(1, scale), Size(1, scale),
                WeaponPalette.SawTeeth);
        }
    }

    private static void DrawShotgun(
        Framebuffer framebuffer, Viewport viewport, int centerX, int baseY, bool muzzleFlash, double scale)
    {
        int bodyWidth = Size(22, scale);
        int bodyHeight = Size(9, scale);
        int left = centerX - bodyWidth / 2;
        int top = baseY - bodyHeight + 1;

        // Приклад шире и «деревянный».
        int extendedHeight = viewport.Rows - 1 - top;
        if (extendedHeight > 0)
        {
            framebuffer.PaintRect(left + Size(4, scale), top, bodyWidth - Size(8, scale), extendedHeight, 0x6A4526);
            framebuffer.PaintRect(left + Size(2, scale), top + Size(2, scale), Size(2, scale), extendedHeight - Size(2, scale), 0x6A4526);
            framebuffer.PaintRect(left + bodyWidth - Size(4, scale), top + Size(2, scale), Size(2, scale), extendedHeight - Size(2, scale), 0x6A4526);
        }

        // Два ствола.
        framebuffer.PaintRect(left + Size(5, scale), top - Size(4, scale), bodyWidth - Size(10, scale), Size(4, scale), 0x222228);
        framebuffer.PaintRect(centerX - Size(2, scale), top - Size(5, scale), Size(1, scale), Size(5, scale), 0x35353E);
        framebuffer.PaintRect(centerX + Size(1, scale), top - Size(5, scale), Size(1, scale), Size(5, scale), 0x35353E);

        if (!muzzleFlash)
            return;

        framebuffer.PaintRect(centerX - Size(5, scale), top - Size(8, scale), Size(4, scale), Size(3, scale), 0xFFD040);
        framebuffer.PaintRect(centerX + Size(1, scale), top - Size(8, scale), Size(4, scale), Size(3, scale), 0xFFD040);
        framebuffer.PaintRect(centerX - Size(6, scale), top - Size(9, scale), Size(12, scale), Size(1, scale), 0xFFFFFF);
    }

    /// <summary>Пулемёт: блок из четырёх стволов, вращающихся после каждого выстрела.</summary>
    private static void DrawChaingun(
        Framebuffer framebuffer, Viewport viewport, Player player, int centerX, int baseY, bool muzzleFlash, double scale)
    {
        int bodyWidth = Size(21, scale);
        int bodyHeight = Size(9, scale);
        int left = centerX - bodyWidth / 2;
        int top = baseY - bodyHeight + 1;

        int height = viewport.Rows - 1 - top;
        if (height > 0)
        {
            framebuffer.PaintRect(left, top, bodyWidth, height, WeaponPalette.ChaingunBody);
            framebuffer.PaintRect(left + Size(3, scale), top + Size(1, scale), bodyWidth - Size(6, scale), height,
                WeaponPalette.ChaingunHousing);
        }

        // Колодка стволов и сами стволы по кругу.
        int muzzleTop = top - Size(4, scale);
        framebuffer.PaintRect(centerX - Size(6, scale), muzzleTop, Size(12, scale), Size(5, scale),
            WeaponPalette.ChaingunMount);

        int ring = Size(3, scale);

        for (int barrel = 0; barrel < 4; barrel++)
        {
            double angle = player.SpinPhase + barrel * Math.PI / 2;
            int offsetX = (int)Math.Round(Math.Cos(angle) * ring);
            int offsetY = (int)Math.Round(Math.Sin(angle) * ring * 0.6);
            int barrelX = centerX + offsetX - Size(1, scale);
            int barrelY = muzzleTop + Size(2, scale) + offsetY;

            framebuffer.PaintRect(barrelX, barrelY - Size(2, scale), Size(2, scale), Size(3, scale),
                WeaponPalette.ChaingunBarrel);

            if (muzzleFlash)
                framebuffer.PaintRect(barrelX, barrelY - Size(3, scale), Size(2, scale), Size(1, scale),
                    WeaponPalette.MuzzleChaingun);
        }

        framebuffer.PaintRect(centerX - Size(7, scale), muzzleTop + Size(5, scale), Size(14, scale), Size(1, scale),
            WeaponPalette.ChaingunRing);

        if (!muzzleFlash)
            return;

        framebuffer.PaintRect(centerX - Size(2, scale), muzzleTop - Size(1, scale), Size(4, scale), Size(2, scale),
            WeaponPalette.MuzzleWarm);
        framebuffer.PaintRect(centerX - Size(1, scale), muzzleTop - Size(2, scale), Size(2, scale), Size(1, scale),
            WeaponPalette.MuzzleCore);
    }

    /// <summary>Ракетница: широкая труба, тёмное жерло и пламя при выстреле.</summary>
    private static void DrawRocketLauncher(
        Framebuffer framebuffer, Viewport viewport, int centerX, int baseY, bool muzzleFlash, double scale)
    {
        int bodyWidth = Size(19, scale);
        int bodyHeight = Size(10, scale);
        int left = centerX - bodyWidth / 2;
        int top = baseY - bodyHeight + 1;

        int height = viewport.Rows - 1 - top;
        if (height > 0)
            framebuffer.PaintRect(left, top, bodyWidth, height, WeaponPalette.RocketTube);

        int tubeWidth = Size(11, scale);
        int tubeHeight = Size(6, scale);
        int tubeLeft = centerX - tubeWidth / 2;
        int tubeTop = top - tubeHeight + Size(2, scale);

        framebuffer.PaintRect(tubeLeft, tubeTop, tubeWidth, tubeHeight, WeaponPalette.RocketTube);
        framebuffer.PaintRect(tubeLeft, tubeTop + tubeHeight - Size(1, scale), tubeWidth, Size(1, scale),
            WeaponPalette.RocketDark);

        // Прицел сверху, маркер готовности и тёмное отверстие жерла.
        framebuffer.PaintRect(tubeLeft + Size(2, scale), tubeTop - Size(3, scale), Size(3, scale), Size(2, scale),
            WeaponPalette.RocketDark);
        framebuffer.PaintRect(tubeLeft + tubeWidth - Size(5, scale), tubeTop - Size(3, scale), Size(3, scale),
            Size(2, scale), WeaponPalette.RocketSight);
        framebuffer.PaintRect(tubeLeft + Size(2, scale), tubeTop + Size(2, scale), tubeWidth - Size(4, scale),
            tubeHeight - Size(3, scale), WeaponPalette.RocketBore);

        if (!muzzleFlash)
            return;

        framebuffer.PaintRect(tubeLeft + Size(1, scale), tubeTop - Size(7, scale), tubeWidth - Size(2, scale),
            Size(4, scale), WeaponPalette.FlameOuter);
        framebuffer.PaintRect(tubeLeft + Size(3, scale), tubeTop - Size(6, scale), tubeWidth - Size(6, scale),
            Size(2, scale), WeaponPalette.FlameInner);
    }

    /// <summary>Плазменная винтовка: пульсирующее ядро и зелёная вспышка у дула.</summary>
    private static void DrawPlasmaRifle(
        Framebuffer framebuffer,
        Viewport viewport,
        Player player,
        int centerX,
        int baseY,
        bool muzzleFlash,
        double nowSeconds,
        double scale)
    {
        int bodyWidth = Size(20, scale);
        int bodyHeight = Size(9, scale);
        int left = centerX - bodyWidth / 2;
        int top = baseY - bodyHeight + 1;

        int height = viewport.Rows - 1 - top;
        if (height > 0)
        {
            framebuffer.PaintRect(left, top, bodyWidth, height, WeaponPalette.PlasmaBody);
            framebuffer.PaintRect(left + Size(4, scale), top, bodyWidth - Size(8, scale), height,
                WeaponPalette.PlasmaHousing);
        }

        // Ядро пульсирует в такт стрельбе, поэтому его размер зависит от времени.
        double pulse = 0.6 + 0.4 * Math.Sin(nowSeconds * 9.0 + player.SpinPhase);
        int coreWidth = Size(2, scale) + (int)Math.Round(pulse * Size(2, scale));
        int coreTop = top - Size(3, scale);

        framebuffer.PaintRect(centerX - coreWidth / 2 - Size(1, scale), coreTop - Size(1, scale),
            coreWidth + Size(2, scale), Size(6, scale), WeaponPalette.PlasmaHousing);
        framebuffer.PaintRect(centerX - coreWidth / 2, coreTop, coreWidth, Size(4, scale), WeaponPalette.PlasmaCore);

        if (!muzzleFlash)
            return;

        framebuffer.PaintRect(centerX - Size(3, scale), coreTop - Size(3, scale), Size(6, scale), Size(3, scale),
            WeaponPalette.PlasmaCore);
        framebuffer.PaintRect(centerX - Size(1, scale), coreTop - Size(4, scale), Size(2, scale), Size(2, scale),
            WeaponPalette.PlasmaGlow);
    }

    /// <summary>BFG9000: огромный раструб, который заряжается между выстрелами.</summary>
    private static void DrawBfg(
        Framebuffer framebuffer,
        Viewport viewport,
        int centerX,
        int baseY,
        bool muzzleFlash,
        double fireProgress,
        double scale)
    {
        int bodyWidth = Size(26, scale);
        int bodyHeight = Size(10, scale);
        int left = centerX - bodyWidth / 2;
        int top = baseY - bodyHeight + 1;

        int height = viewport.Rows - 1 - top;
        if (height > 0)
        {
            framebuffer.PaintRect(left, top, bodyWidth, height, WeaponPalette.BfgBody);
            framebuffer.PaintRect(left + Size(5, scale), top, bodyWidth - Size(10, scale), height,
                WeaponPalette.BfgHousing);
        }

        int muzzleWidth = Size(16, scale);
        int muzzleHeight = Size(7, scale);
        int muzzleLeft = centerX - muzzleWidth / 2;
        int muzzleTop = top - muzzleHeight + Size(2, scale);

        framebuffer.PaintRect(muzzleLeft, muzzleTop, muzzleWidth, muzzleHeight, WeaponPalette.BfgMuzzle);

        // Ядро растёт по мере перезарядки — от выстрела до готовности.
        int coreWidth = Size(4, scale) + (int)Math.Round(fireProgress * Size(6, scale));
        int coreHeight = Size(2, scale) + (int)Math.Round(fireProgress * Size(3, scale));

        framebuffer.PaintRect(centerX - coreWidth / 2, muzzleTop + (muzzleHeight - coreHeight) / 2, coreWidth,
            coreHeight, WeaponPalette.BfgCore);

        if (!muzzleFlash)
            return;

        framebuffer.PaintRect(muzzleLeft - Size(2, scale), muzzleTop - Size(4, scale), muzzleWidth + Size(4, scale),
            Size(4, scale), WeaponPalette.BfgMuzzle);
        framebuffer.PaintRect(centerX - Size(3, scale), muzzleTop - Size(5, scale), Size(6, scale), Size(3, scale),
            WeaponPalette.BfgCore);
    }

    /// <summary>Масштабирует базовый размер под высоту кадра, минимум — 1 символ.</summary>
    private static int Size(double baseSize, double scale) =>
        Math.Max(1, (int)Math.Round(baseSize * scale));
}
