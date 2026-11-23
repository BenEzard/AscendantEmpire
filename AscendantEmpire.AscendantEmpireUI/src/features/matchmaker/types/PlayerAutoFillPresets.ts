// Dev-only answers for the player definition dialog, so a test game's slots can be filled in with one press each.

import { DistinctColors } from "@/components/ui";
import type { RulerGender } from "./RulerGender";
import { RULER_PORTRAITS } from "./RulerPortrait";

/** One set of answers, written as the dialog shows them: the portrait by its label and the colour by its Tailwind name. */
export type PlayerAutoFillPreset = {
  rulerName: string;
  rulerGender: RulerGender;
  rulerTitle: string;
  rulerPortraitLabel: string;
  factionName: string;
  factionColourName: string;
  peopleName: string;
  demonym: string;
  language: string;
};

/** A preset turned into the values the dialog stores: a portrait path and a colour value. */
export type ResolvedPlayerAutoFillPreset = Omit<PlayerAutoFillPreset, "rulerPortraitLabel" | "factionColourName"> & {
  rulerPortraitPath: string;
  factionColour: string;
};

/** Applied in order by successive presses of Auto-Fill, wrapping back to the first. */
export const PLAYER_AUTO_FILL_PRESETS: readonly PlayerAutoFillPreset[] = [
  {
    rulerName: "Ben",
    rulerGender: "Male",
    rulerTitle: "Supreme Commander",
    rulerPortraitLabel: "Western King",
    factionName: "Roman Empire",
    factionColourName: "purple-700",
    peopleName: "Romans",
    demonym: "Roman",
    language: "Italian",
  },
  {
    rulerName: "Geoff",
    rulerGender: "Male",
    rulerTitle: "King",
    rulerPortraitLabel: "Viking King",
    factionName: "Vikings",
    factionColourName: "yellow-400",
    peopleName: "Vikings",
    demonym: "Viking",
    language: "English",
  },
  {
    rulerName: "Jen",
    rulerGender: "Female",
    rulerTitle: "Princess",
    rulerPortraitLabel: "Egyptian Queen",
    factionName: "Sheepites",
    factionColourName: "sky-500",
    peopleName: "Flock",
    demonym: "Sheep",
    language: "English",
  },
];

/**
 * Looks up the portrait path and colour value a preset names.
 *
 * @throws When the portrait is not offered for the preset's gender or the colour is not in the faction palette, so a renamed file or swatch fails
 * loudly rather than auto-filling a blank.
 */
export function resolvePlayerAutoFillPreset(preset: PlayerAutoFillPreset): ResolvedPlayerAutoFillPreset {
  const { rulerPortraitLabel, factionColourName, ...names } = preset;

  const portrait = RULER_PORTRAITS.find((candidate) => candidate.label === rulerPortraitLabel && candidate.gender === preset.rulerGender);
  const colour = DistinctColors.AllColors.find((candidate) => candidate.description === factionColourName);

  if (portrait === undefined) {
    throw new Error(`No ${preset.rulerGender} ruler portrait is labelled "${rulerPortraitLabel}".`);
  }

  if (colour === undefined) {
    throw new Error(`The faction palette has no colour named "${factionColourName}".`);
  }

  return { ...names, rulerPortraitPath: portrait.path, factionColour: colour.value };
}
