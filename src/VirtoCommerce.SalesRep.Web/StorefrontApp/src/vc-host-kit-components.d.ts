// Kit components the host registers globally (`app.use(uiKit)`) but the facade contract does not type yet.
// Props mirror the host ui-kit sources (vc-frontend-next `client-app/ui-kit/components/**`); drop an entry
// once the facade types that component.
import type { DefineComponent, SlotsType, VNode } from "vue";

type VcTooltipTriggerPropsType = Record<string, unknown>;

declare module "vue" {
  interface GlobalComponents {
    VcSwitch: DefineComponent<
      {
        modelValue?: boolean;
        disabled?: boolean;
        name?: string;
        value?: boolean;
        color?: VcMainColorType;
        size?: "xs" | "sm" | "md" | "lg";
        labelPosition?: "left" | "right";
        label?: string;
        ariaLabel?: string;
        testId?: string;
      },
      object,
      object,
      object,
      object,
      object,
      object,
      { "update:modelValue": (value?: boolean) => void; change: (value: boolean) => void }
    >;
    VcTabSwitchGroup: DefineComponent<{
      variant?: "plain" | "filled" | "filled-strong" | "seg";
      fill?: boolean;
      ariaLabel?: string;
    }>;
    VcTooltip: DefineComponent<
      {
        placement?: string;
        strategy?: "absolute" | "fixed";
        disabled?: boolean;
        hover?: boolean;
        width?: string;
        enableTeleport?: boolean;
        teleportSelector?: string;
      },
      object,
      object,
      object,
      object,
      object,
      object,
      { shown: (isShown: boolean) => void },
      string,
      object,
      object,
      object,
      SlotsType<{
        default: (scope: { opened: boolean; triggerProps: VcTooltipTriggerPropsType; tooltipId: string }) => VNode[];
        trigger: () => VNode[];
        content: () => VNode[];
      }>
    >;
    VcPriceDisplay: DefineComponent<{ value?: { formattedAmount: string } }>;
  }
}

export {};
