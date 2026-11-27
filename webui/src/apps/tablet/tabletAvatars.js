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

export function avatarSrc(key) {
  return SOURCES[key] || SOURCES.Someone;
}

export function personalColor(key) {
  return PERSONAL_COLOR[key] || '#ffffff';
}
