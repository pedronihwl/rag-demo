import { Card, CardHeader } from "@/components/ui/card";
import { Label } from "@/components/ui/label";
import { DbFile, DbFileStatus, Map } from "@/types/@types";
import { Trash } from "@phosphor-icons/react";
import clsx from "clsx";
import { CircleCheck, Loader2, MinusCircle } from "lucide-react";
import { ElementType } from "react";

type IProps = {
    file: DbFile;
    onDelete: (file: DbFile) => void;
}

const getStatus: Map<DbFileStatus, { color: string, label: string, icon: ElementType }> = {
    "Processed": { color: "#22c55e", label: "Processado", icon: CircleCheck },
    "NotProcessed": { color: "#71717a", label: "Criado", icon: MinusCircle },
    "Processing": { color: "#eab308", label: "Processando", icon: Loader2 },
    "ProcessingFailed": { color: "#b91c1c", label: "Falhou", icon: CircleCheck }
}

const FileContainer = ({ file, onDelete }: IProps) => {
    const { label, color, icon: Icon } = getStatus[file.status]
    const value = file.pages === 0 ? 0 : Math.round((file.processedPages / file.pages) * 100)

    return <Card>
        <CardHeader>
            <div className="flex justify-between">
                <Label className="truncate flex-grow">{file.name}</Label>
                <Trash size={18} className="hover:cursor-pointer hover:text-red-900" onClick={() => onDelete(file)} />
            </div>
            <div>
                <div className="flex">
                    <Icon color={color} className={clsx(
                        'mt-1 mr-2 h-4 w-4',
                        {
                            'animate-spin': file.status === "Processing"
                        })} />
                    <span style={{ color }}>{label}</span>
                    <p className="ml-5">{value}%</p>
                </div>
            </div>
        </CardHeader>
    </Card>

}

export default FileContainer;