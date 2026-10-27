// Kit components the host registers globally (`app.use(uiKit)`) but the facade contract does not type yet.
// Props mirror the host ui-kit sources (vc-frontend `client-app/ui-kit/components/**`); drop an entry once the
// facade types that component.
import type { DefineComponent } from "vue";

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
    VcPriceDisplay: DefineComponent<{ value?: { formattedAmount: string } }>;
  }
}

export {};
