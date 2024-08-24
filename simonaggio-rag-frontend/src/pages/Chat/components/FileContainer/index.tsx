import { Badge } from "@/components/ui/badge";
import { Card } from "@/components/ui/card";
import { Label } from "@/components/ui/label";
import { Progress } from "@/components/ui/progress";
import { Trash } from "@phosphor-icons/react";


export type FileType = {
    name: string;
    status: string;
    totalPages: number;
    processedPages: number;
    chunks: number;
}

type IProps = {
    file: FileType;
    onDelete: (file: FileType) => void;
}

const convertStatus = (status: string): { label: string, color: string } => {

    switch (status) {
        case "NOT_PROCESSED":
            return {
                label: "Criado",
                color: ""
            }
        case "PROCESSING":
            return {
                label: "Processando",
                color: ""
            }
        case "PROCESSED":
            return {
                label: "Processado",
                color: ""
            }

        default:
            return {
                label: "Desconhecido",
                color: ""
            }
    }
}

const FileContainer = ({ file, onDelete }: IProps) => {
    const { label, color } = convertStatus(file.status)
    const value = (file.processedPages / file.totalPages) * 100

    return <Card className="px-4 py-2">
        <div className="flex justify-between">
            <Badge>{label}</Badge>
            <Badge>{file.chunks} Chunks</Badge>
            <Trash size={18} onClick={() => onDelete(file)} weight="light" className="mt-0.5 hover:cursor-pointer hover:text-red-900" />
        </div>
        <p className="leading-7">{file.name}</p>
        <div>
            <Label className="ml-2">{file.processedPages}/{file.totalPages} Páginas</Label>
            <Progress value={value} className={`${color}`}></Progress>
        </div>
    </Card>

}

export default FileContainer;