import { zodResolver } from "@hookform/resolvers/zod";
import { useForm } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { z } from "zod";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  Form,
  FormControl,
  FormDescription,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from "@/components/ui/form";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { matchConflictCode, toastFormSubmitError } from "@/lib/apiErrors";
import type { Tenant, TenantInput } from "./types";
import { useCreateTenant, useUpdateTenant } from "./useTenants";

function buildSchema(t: (k: string) => string) {
  return z.object({
    name: z
      .string()
      .trim()
      .min(1, t("tenants.errors.nameRequired"))
      .max(200, t("tenants.errors.nameTooLong")),
    maxConcurrentLicenses: z
      .number({ message: t("tenants.errors.limitInteger") })
      .int(t("tenants.errors.limitInteger"))
      .min(0, t("tenants.errors.limitRange"))
      .max(100_000, t("tenants.errors.limitRange")),
    isActive: z.boolean(),
  });
}

type FormValues = z.infer<ReturnType<typeof buildSchema>>;

interface TenantFormDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  tenant?: Tenant | null;
}

export function TenantFormDialog({ open, onOpenChange, tenant }: TenantFormDialogProps) {
  const { t } = useTranslation();
  const isEdit = Boolean(tenant);

  const create = useCreateTenant();
  const update = useUpdateTenant();

  const form = useForm<FormValues>({
    resolver: zodResolver(buildSchema(t)),
    defaultValues: tenant
      ? {
          name: tenant.name,
          maxConcurrentLicenses: tenant.maxConcurrentLicenses,
          isActive: tenant.isActive,
        }
      : { name: "", maxConcurrentLicenses: 0, isActive: true },
  });

  const onSubmit = form.handleSubmit(async (values) => {
    const input: TenantInput = {
      name: values.name.trim(),
      maxConcurrentLicenses: values.maxConcurrentLicenses,
      isActive: values.isActive,
    };

    try {
      if (tenant) {
        await update.mutateAsync({ id: tenant.id, input });
        toast.success(t("tenants.toasts.updated", { name: input.name }));
      } else {
        await create.mutateAsync(input);
        toast.success(t("tenants.toasts.created", { name: input.name }));
      }
      onOpenChange(false);
    } catch (error) {
      const mapped = matchConflictCode(error, {
        NAME_DUPLICATE: { field: "name", messageKey: "tenants.errors.nameDuplicate" } as const,
      });
      if (mapped) {
        form.setError(mapped.field, { type: "server", message: t(mapped.messageKey) });
        return;
      }
      toastFormSubmitError(error, t);
    }
  });

  const pending = create.isPending || update.isPending;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>
            {isEdit ? t("tenants.form.editTitle") : t("tenants.form.createTitle")}
          </DialogTitle>
          <DialogDescription>{t("tenants.form.subtitle")}</DialogDescription>
        </DialogHeader>

        <Form {...form}>
          <form onSubmit={onSubmit} noValidate className="grid gap-4">
            <FormField
              control={form.control}
              name="name"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t("tenants.fields.name")}</FormLabel>
                  <FormControl>
                    <Input
                      autoFocus
                      autoComplete="off"
                      placeholder={t("tenants.form.namePlaceholder")}
                      {...field}
                    />
                  </FormControl>
                  <FormMessage />
                </FormItem>
              )}
            />
            <FormField
              control={form.control}
              name="maxConcurrentLicenses"
              render={({ field }) => (
                <FormItem>
                  <FormLabel>{t("tenants.fields.maxConcurrentLicenses")}</FormLabel>
                  <FormControl>
                    <Input
                      type="number"
                      min={0}
                      max={100_000}
                      step={1}
                      inputMode="numeric"
                      value={field.value}
                      onChange={(e) => {
                        const raw = e.target.value;
                        field.onChange(raw === "" ? 0 : Number(raw));
                      }}
                    />
                  </FormControl>
                  <FormDescription>{t("tenants.form.limitHint")}</FormDescription>
                  <FormMessage />
                </FormItem>
              )}
            />
            <FormField
              control={form.control}
              name="isActive"
              render={({ field }) => (
                <FormItem>
                  <div className="flex items-center justify-between gap-3 rounded-md border px-3 py-2">
                    <div className="grid gap-0.5">
                      <Label htmlFor="tenant-isActive" className="font-medium">
                        {t("tenants.fields.isActive")}
                      </Label>
                      <p className="text-muted-foreground text-xs">
                        {t("tenants.form.isActiveHint")}
                      </p>
                    </div>
                    <input
                      id="tenant-isActive"
                      type="checkbox"
                      className="size-4 cursor-pointer"
                      checked={field.value}
                      onChange={(e) => field.onChange(e.target.checked)}
                    />
                  </div>
                  <FormMessage />
                </FormItem>
              )}
            />

            <DialogFooter className="gap-2">
              <Button
                type="button"
                variant="ghost"
                disabled={pending}
                onClick={() => onOpenChange(false)}
              >
                {t("common.cancel")}
              </Button>
              <Button type="submit" disabled={pending}>
                {pending ? t("common.loading") : isEdit ? t("common.save") : t("common.create")}
              </Button>
            </DialogFooter>
          </form>
        </Form>
      </DialogContent>
    </Dialog>
  );
}
