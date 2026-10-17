/**
 * 平板电脑的头像与人物配色。
 * 头像由 SD 原图裁切（见 avatars/，裁切框 760×760 @ (644,260)，缩放到 192px）。
 * Madeline 与 Mastermind 共用资源，后端已把 Madeline 映射为键 "Mastermind"（见 Game/WebTablet/WebTabletAvatar.cs）。
 */
import avatarBlack from './avatars/Black.png';
import avatarHasung from './avatars/Hasung.png';
import avatarJeremy from './avatars/Jeremy.png';
import avatarKaho from './avatars/Kaho.png';
import avatarLian from './avatars/Lian.png';
import avatarLiliana from './avatars/Liliana.png';
import avatarLouis from './avatars/Louis.png';
import avatarLuna from './avatars/Luna.png';
import avatarMastermind from './avatars/Mastermind.png';
import avatarMiyuki from './avatars/Miyuki.png';
import avatarNoel from './avatars/Noel.png';
import avatarRin from './avatars/Rin.png';
import avatarSeol from './avatars/Seol.png';
import avatarSoi from './avatars/Soi.png';
import avatarSomeone from './avatars/Someone.png';

const SOURCES = {
  Black: avatarBlack,
  Hasung: avatarHasung,
  Jeremy: avatarJeremy,
  Kaho: avatarKaho,
  Lian: avatarLian,
  Liliana: avatarLiliana,
  Louis: avatarLouis,
  Luna: avatarLuna,
  Mastermind: avatarMastermind,
  Miyuki: avatarMiyuki,
  Noel: avatarNoel,
  Rin: avatarRin,
  Seol: avatarSeol,
  Soi: avatarSoi,
  Someone: avatarSomeone,
};

/** 与游戏 PersonalColor.cs 一致的气泡色；未列出的（Mastermind、Black、Someone）为白色。 */
const PERSONAL_COLOR = {
  Soi: '#959BE3',
  Jeremy: '#D8D3AB',
  Rin: '#7E53AE',
  Seol: '#81674D',
  Louis: '#7BAB4C',
  Lian: '#999584',
  Noel: '#cdcdcd',
  Liliana: '#FFA3D3',
  Kaho: '#BC3B3B',
  Hasung: '#715D76',
  Luna: '#65E3D4',
  Miyuki: '#F4D278',
};

/** 头像地址；未知键回退到无名剪影。 */
export function avatarSrc(key) {
  return SOURCES[key] || SOURCES.Someone;
}

/** 气泡底色；未知键或无专属色时为白。 */
export function personalColor(key) {
  return PERSONAL_COLOR[key] || '#ffffff';
}
